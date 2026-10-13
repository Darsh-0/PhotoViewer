using Microsoft.AspNetCore.Mvc;

namespace PictureSite.Api.Photos;

public static class PhotoEndpoints
{
    private const long MaxImageSize = 10 * 1024 * 1024;
    public const long MaxRequestSize = 100 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/images", ListImages);
        endpoints.MapPost("/api/images", UploadImages).DisableAntiforgery();
        endpoints.MapDelete("/api/images/{id}", DeleteImage);
        endpoints.MapGet("/images/{id}", GetImage);
        return endpoints;
    }

    private static async Task<IResult> ListImages(IPhotoStore photos, CancellationToken cancellationToken)
    {
        var ids = await photos.ListAsync(cancellationToken);
        return Results.Ok(ids.Select(id => new PhotoResponse(id, $"/images/{Uri.EscapeDataString(id)}")));
    }

    private static async Task<IResult> UploadImages(
        [FromForm(Name = "image")] IFormFileCollection images,
        IPhotoStore photos,
        CancellationToken cancellationToken)
    {
        if (images.Count == 0 || images.Any(image => image.Length == 0))
            return Results.BadRequest(new { error = "Choose an image to upload." });
        if (images.Any(image => image.Length > MaxImageSize))
            return Results.BadRequest(new { error = "Images must be 10 MB or smaller." });

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
            uploadedPhotos.Add(new PhotoResponse(id, $"/images/{Uri.EscapeDataString(id)}"));
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

    private sealed record PhotoResponse(string Id, string Url);
}
