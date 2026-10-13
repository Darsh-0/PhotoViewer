namespace PictureSite.Api.Photos;

public interface IPhotoStore
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);
    Task<PhotoFile?> OpenAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record PhotoFile(Stream Content, string ContentType);

public static class PhotoTypes
{
    public static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
        };
}
