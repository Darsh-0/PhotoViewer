namespace PictureSite.Api.Photos;

public sealed class LocalPhotoStore : IPhotoStore
{
    private readonly string _directory;

    public LocalPhotoStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
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

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(id);
        if (path is null || !File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
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
