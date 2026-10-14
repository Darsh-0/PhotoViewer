using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PictureSite.Api.Photos;

public sealed class LocalPhotoStore : IPhotoStore
{
    private readonly string _directory;
    private readonly string _thumbnailDirectory;

    public LocalPhotoStore(string directory)
    {
        _directory = directory;
        _thumbnailDirectory = Path.Combine(directory, ".thumbnails");
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(_thumbnailDirectory);
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> ids = Directory.EnumerateFiles(_directory)
            .Select(Path.GetFileName)
            .Where(name => name is not null && PhotoTypes.ContentTypes.ContainsKey(Path.GetExtension(name)))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult(ids);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        if (!PhotoTypes.ContentTypes.ContainsKey(extension))
            throw new ArgumentException("Unsupported photo extension.", nameof(extension));

        var id = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(_directory, id);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, cancellationToken);
        return id;
    }

    public Task<PhotoFile?> OpenAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(id);
        if (path is null || !File.Exists(path)) return Task.FromResult<PhotoFile?>(null);

        var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = new PhotoFile(content, PhotoTypes.ContentTypes[Path.GetExtension(path)]);
        return Task.FromResult<PhotoFile?>(result);
    }

    public async Task<PhotoFile?> OpenThumbnailAsync(string id, CancellationToken cancellationToken = default)
    {
        var sourcePath = ResolvePath(id);
        if (sourcePath is null || !File.Exists(sourcePath)) return null;

        var thumbnailPath = Path.Combine(_thumbnailDirectory, $"{id}.webp");
        if (!File.Exists(thumbnailPath))
        {
            var temporaryPath = Path.Combine(_thumbnailDirectory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                using var image = await Image.LoadAsync(sourcePath, cancellationToken);
                for (var frame = image.Frames.Count - 1; frame > 0; frame--)
                    image.Frames.RemoveFrame(frame);

                image.Mutate(operation => operation
                    .AutoOrient()
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(640, 640),
                        Mode = ResizeMode.Max,
                    }));

                await image.SaveAsWebpAsync(temporaryPath, new WebpEncoder
                {
                    Quality = 76,
                    SkipMetadata = true,
                }, cancellationToken);
                File.Move(temporaryPath, thumbnailPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        var content = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new PhotoFile(content, "image/webp");
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(id);
        if (path is null || !File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        var thumbnailPath = Path.Combine(_thumbnailDirectory, $"{id}.webp");
        if (File.Exists(thumbnailPath)) File.Delete(thumbnailPath);
        return Task.FromResult(true);
    }

    private string? ResolvePath(string id)
    {
        var extension = Path.GetExtension(id);
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id) ||
            !PhotoTypes.ContentTypes.ContainsKey(extension)) return null;
        return Path.Combine(_directory, id);
    }
}
