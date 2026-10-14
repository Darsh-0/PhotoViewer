using System.IO.Compression;
using System.Net.WebSockets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PictureSite.Api.Photos;

public static class PhotoEndpoints
{
    private const long MaxImageSize = 100L * 1024 * 1024;
    public const long MaxRequestSize = 10000L * 1024 * 1024;

    public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/images", ListImages);
        endpoints.MapGet("/ws/images", HandlePhotoUpdates);
        endpoints.MapGet("/api/images/download", DownloadAllImages);
        endpoints.MapPost("/api/images/download", DownloadSelectedImages);
        endpoints.MapPost("/api/images", UploadImages).DisableAntiforgery();
        endpoints.MapDelete("/api/images/{id}", DeleteImage).RequireAuthorization("AdminOnly");
        endpoints.MapGet("/images/{id}/thumbnail", GetThumbnail);
        endpoints.MapGet("/images/{id}/download", DownloadImageAsPng);
        endpoints.MapGet("/images/{id}", GetImage);
        return endpoints;
    }

    private const int DefaultPageSize = 24;
    private const int MaxPageSize = 48;

    private static async Task<IResult> HandlePhotoUpdates(HttpContext context, PhotoUpdateHub updates)
    {
        if (!context.WebSockets.IsWebSocketRequest)
            return Results.BadRequest(new { error = "A WebSocket connection is required." });

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        updates.Add(socket);
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var received = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (received.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The browser disconnected or the server is shutting down.
        }
        catch (WebSocketException)
        {
            // The browser disconnected without a close frame.
        }
        finally
        {
            updates.Remove(socket);
        }

        if (socket.State == WebSocketState.CloseReceived)
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        return Results.Empty;
    }

    private static async Task<IResult> ListImages(
        int? offset,
        int? limit,
        string? after,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        var ids = await photos.ListAsync(cancellationToken);
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var start = after is null ? Math.Clamp(offset ?? 0, 0, ids.Count) : 0;
        var candidates = after is null
            ? ids.Skip(start)
            : ids.Where(id => string.Compare(id, after, StringComparison.OrdinalIgnoreCase) > 0);
        var pageItems = candidates.Take(pageSize + 1).ToArray();
        var items = pageItems.Take(pageSize)
            .Select(id => new PhotoResponse(
                id,
                $"/images/{Uri.EscapeDataString(id)}",
                $"/images/{Uri.EscapeDataString(id)}/thumbnail",
                $"/images/{Uri.EscapeDataString(id)}/download"));
        var hasMore = pageItems.Length > pageSize;
        return Results.Ok(new ImagePage(items, ids.Count, hasMore));
    }

    private static async Task<IResult> DownloadAllImages(IPhotoStore photos, CancellationToken cancellationToken)
    {
        var ids = await photos.ListAsync(cancellationToken);
        return await CreateArchive(ids, photos, cancellationToken);
    }

    private static async Task<IResult> DownloadSelectedImages(
        DownloadRequest request,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Length == 0)
            return Results.BadRequest(new { error = "Select at least one photo to download." });
        return await CreateArchive(request.Ids.Distinct(StringComparer.Ordinal), photos, cancellationToken);
    }

    private static async Task<IResult> CreateArchive(
        IEnumerable<string> ids,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = new MemoryStream();
        var photosAdded = 0;
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var id in ids)
            {
                var photo = await photos.OpenAsync(id, cancellationToken);
                if (photo is null) continue;

                await using (photo.Content)
                {
                    var fileName = $"{Path.GetFileNameWithoutExtension(id)}.png";
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await SaveAsPngAsync(photo.Content, entryStream, cancellationToken);
                    photosAdded++;
                }
            }
        }

        if (photosAdded == 0)
            return Results.NotFound(new { error = "No photos were found to download." });

        return Results.File(archiveStream.ToArray(), "application/zip", "photos.zip");
    }

    private static async Task<IResult> UploadImages(
        HttpRequest request,
        IPhotoStore photos,
        PhotoUpdateHub updates,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var images = form.Files;
        if (images.Count == 0 || images.Any(image => image.Length == 0))
            return Results.BadRequest(new { error = "Choose an image to upload." });
        if (images.Any(image => image.Length > MaxImageSize))
            return Results.BadRequest(new { error = "Images must be 100 MB or smaller." });

        var validatedImages = new List<(IFormFile Image, string Extension)>();
        foreach (var image in images)
        {
            var extension = Path.GetExtension(Path.GetFileName(image.FileName)).ToLowerInvariant();
            if (!PhotoTypes.ContentTypes.TryGetValue(extension, out var contentType) || image.ContentType != contentType)
                return Results.BadRequest(new { error = "Use a JPG, PNG, GIF, or WebP image." });
            validatedImages.Add((image, extension));
        }

        var uploadedPhotos = new List<PhotoResponse>(validatedImages.Count);
        foreach (var (image, extension) in validatedImages)
        {
            await using var input = image.OpenReadStream();
            var id = await photos.SaveAsync(input, extension, cancellationToken);
            var photo = new PhotoResponse(
                id,
                $"/images/{Uri.EscapeDataString(id)}",
                $"/images/{Uri.EscapeDataString(id)}/thumbnail",
                $"/images/{Uri.EscapeDataString(id)}/download");
            uploadedPhotos.Add(photo);
            await updates.BroadcastPhotoAddedAsync(photo, cancellationToken);
        }

        return Results.Created("/api/images", uploadedPhotos);
    }

    private static async Task<IResult> DeleteImage(
        string id,
        IPhotoStore photos,
        CancellationToken cancellationToken) =>
        await photos.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> GetImage(
        string id,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        var photo = await photos.OpenAsync(id, cancellationToken);
        return photo is null ? Results.NotFound() : Results.File(photo.Content, photo.ContentType);
    }

    private static async Task<IResult> DownloadImageAsPng(
        string id,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        var photo = await photos.OpenAsync(id, cancellationToken);
        if (photo is null) return Results.NotFound();

        await using (photo.Content)
        await using (var png = new MemoryStream())
        {
            await SaveAsPngAsync(photo.Content, png, cancellationToken);
            var fileName = $"{Path.GetFileNameWithoutExtension(id)}.png";
            return Results.File(png.ToArray(), "image/png", fileName);
        }
    }

    private static async Task SaveAsPngAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);
        for (var frame = image.Frames.Count - 1; frame > 0; frame--)
            image.Frames.RemoveFrame(frame);
        image.Mutate(operation => operation.AutoOrient());
        await image.SaveAsPngAsync(output, cancellationToken);
    }

    private static async Task<IResult> GetThumbnail(
        string id,
        HttpContext context,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        var photo = await photos.OpenThumbnailAsync(id, cancellationToken);
        if (photo is not null)
            context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return photo is null ? Results.NotFound() : Results.File(photo.Content, photo.ContentType);
    }

    private sealed record PhotoResponse(string Id, string Url, string ThumbnailUrl, string DownloadUrl);
    private sealed record ImagePage(IEnumerable<PhotoResponse> Items, int Total, bool HasMore);
    private sealed record DownloadRequest(string[]? Ids);
}
