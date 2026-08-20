using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Storage;

public sealed class LocalDiskDerivativeImageCache : IDerivativeImageCache
{
    private readonly ImageProcessingOptions _options;

    public LocalDiskDerivativeImageCache(IOptions<ImageProcessingOptions> options) => _options = options.Value;

    public Task<Stream?> TryOpenReadAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.DerivativeCacheRootPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        string path = GetCachePath(cacheKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public async Task WriteAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.DerivativeCacheRootPath))
        {
            return;
        }

        string path = GetCachePath(cacheKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        content.Position = 0;
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    private string GetCachePath(string cacheKey)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(_options.DerivativeCacheRootPath!, hash[..2], hash);
    }
}
