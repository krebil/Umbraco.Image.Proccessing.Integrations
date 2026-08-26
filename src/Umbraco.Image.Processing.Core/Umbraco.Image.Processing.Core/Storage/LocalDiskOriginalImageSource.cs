using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Storage;

public sealed class LocalDiskOriginalImageSource(IOptions<ImageProcessingOptions> options) : IOriginalImageSource
{
    private readonly ImageProcessingOptions _options = options.Value;

    public Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(_options.OriginalsRootPath);
        string relative = requestPath.TrimStart('/', '\\');
        string fullPath = Path.GetFullPath(Path.Combine(root, relative));

        // Reject any path that escapes the configured root (e.g. via "../" segments).
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }
}
