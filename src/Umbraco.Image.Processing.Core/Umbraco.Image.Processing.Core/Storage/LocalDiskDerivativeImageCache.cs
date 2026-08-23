using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Storage;

public sealed class LocalDiskDerivativeImageCache : IDerivativeImageCache
{
    private readonly ImageProcessingOptions _options;
    private readonly TimeProvider _timeProvider;

    public LocalDiskDerivativeImageCache(IOptions<ImageProcessingOptions> options, TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<Stream?> TryOpenReadAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.DerivativeCacheRootPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        string path = GetCachePath(cacheKey);
        if (!File.Exists(path) || IsExpired(path))
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

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_options.DerivativeCacheRootPath) && Directory.Exists(_options.DerivativeCacheRootPath))
        {
            foreach (string directory in Directory.EnumerateDirectories(_options.DerivativeCacheRootPath))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    public Task EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.DerivativeCacheRootPath) || !Directory.Exists(_options.DerivativeCacheRootPath))
        {
            return Task.CompletedTask;
        }

        foreach (string path in Directory.EnumerateFiles(_options.DerivativeCacheRootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExpired(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsExpired(string path)
    {
        var writtenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        return _timeProvider.GetUtcNow() - writtenAt > _options.CacheControlMaxAge;
    }

    private string GetCachePath(string cacheKey)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(_options.DerivativeCacheRootPath!, hash[..2], hash);
    }
}
