using System.IO.Compression;
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

    private static async Task<IResult> ListImages(
        int? offset,
        int? limit,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        var ids = await photos.ListAsync(cancellationToken);
        var start = Math.Clamp(offset ?? 0, 0, ids.Count);
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var items = ids.Skip(start).Take(pageSize)
            .Select(id => new PhotoResponse(
                id,
                $"/images/{Uri.EscapeDataString(id)}",
                $"/images/{Uri.EscapeDataString(id)}/thumbnail",
                $"/images/{Uri.EscapeDataString(id)}/download"));
        return Results.Ok(new ImagePage(items, ids.Count, start + pageSize < ids.Count));
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
            uploadedPhotos.Add(new PhotoResponse(
                id,
                $"/images/{Uri.EscapeDataString(id)}",
                $"/images/{Uri.EscapeDataString(id)}/thumbnail",
                $"/images/{Uri.EscapeDataString(id)}/download"));
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
