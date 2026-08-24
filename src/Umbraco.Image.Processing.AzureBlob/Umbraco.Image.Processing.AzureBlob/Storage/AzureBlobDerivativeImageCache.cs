using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.AzureBlob.Storage;

/// <summary>
/// <see cref="IDerivativeImageCache" /> backed by Azure Blob Storage, so the standalone Service can
/// run multiple instances behind a load balancer sharing one derivative cache. Age tracking rides
/// each blob's own <c>Last-Modified</c> timestamp (set on every upload, including overwrites) — the
/// same approach <c>LocalDiskDerivativeImageCache</c> takes with filesystem <c>LastWriteTimeUtc</c>,
/// so no extra metadata/side-channel is needed for TTL eviction (ADR-0007).
/// </summary>
public sealed class AzureBlobDerivativeImageCache : IDerivativeImageCache
{
    private readonly ImageProcessingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Lazy<Task<BlobContainerClient>> _containerClient;

    public AzureBlobDerivativeImageCache(
        IOptions<ImageProcessingOptions> options,
        IOptions<AzureBlobCacheOptions> blobOptions,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;

        AzureBlobCacheOptions blob = blobOptions.Value;
        _containerClient = new Lazy<Task<BlobContainerClient>>(async () =>
        {
            var client = new BlobContainerClient(blob.ConnectionString, blob.ContainerName);
            await client.CreateIfNotExistsAsync();
            return client;
        });
    }

    public async Task<Stream?> TryOpenReadAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        BlobContainerClient container = await _containerClient.Value.WaitAsync(cancellationToken);
        BlobClient blob = container.GetBlobClient(GetBlobName(cacheKey));

        BlobProperties properties;
        try
        {
            properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        if (IsExpired(properties.LastModified))
        {
            return null;
        }

        BlobDownloadStreamingResult download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return download.Content;
    }

    public async Task WriteAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default)
    {
        BlobContainerClient container = await _containerClient.Value.WaitAsync(cancellationToken);
        BlobClient blob = container.GetBlobClient(GetBlobName(cacheKey));

        content.Position = 0;
        await blob.UploadAsync(content, overwrite: true, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        BlobContainerClient container = await _containerClient.Value.WaitAsync(cancellationToken);

        await foreach (BlobItem item in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            await container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
        }
    }

    public async Task EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        BlobContainerClient container = await _containerClient.Value.WaitAsync(cancellationToken);

        await foreach (BlobItem item in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (IsExpired(item.Properties.LastModified))
            {
                await container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
            }
        }
    }

    private bool IsExpired(DateTimeOffset? lastModified) =>
        lastModified is { } modified && _timeProvider.GetUtcNow() - modified > _options.CacheControlMaxAge;

    private static string GetBlobName(string cacheKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
}
