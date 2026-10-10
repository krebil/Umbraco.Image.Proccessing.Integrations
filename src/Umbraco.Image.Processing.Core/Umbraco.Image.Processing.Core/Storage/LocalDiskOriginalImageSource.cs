namespace Umbraco.Image.Processing.Core.Storage;

public sealed class LocalDiskOriginalImageSource(string rootPath) : IOriginalImageSource
{
    // Ends in a separator, so a sibling such as ".../media-archive" doesn't pass for ".../media".
    private readonly string _root = WithTrailingSeparator(Path.GetFullPath(rootPath));

    public Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default)
    {
        string relative = requestPath.TrimStart('/', '\\');
        string fullPath = Path.GetFullPath(Path.Combine(_root, relative));

        // Reject any path that escapes the configured root (e.g. via "../" segments).
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    private static string WithTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
}
