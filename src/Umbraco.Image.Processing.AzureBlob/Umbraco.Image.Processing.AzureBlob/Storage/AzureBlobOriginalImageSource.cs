using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.AzureBlob.Storage;

/// <summary>
/// <see cref="IOriginalImageSource" /> backed by Azure Blob Storage — reads the original (unprocessed)
/// media Umbraco itself wrote, directly from the same container Umbraco's own Blob-backed media file
/// system (e.g. <c>Umbraco.StorageProviders.AzureBlob</c>) uses. This is a different responsibility
/// from <see cref="AzureBlobDerivativeImageCache" />: that class caches already-processed crops this
/// product produced; this class resolves the raw source those crops are made from, and never writes.
/// </summary>
/// <remarks>
/// Paths with <c>.</c> or <c>..</c> segments are refused: the SDK builds the blob URI with
/// <see cref="Uri" />, which collapses them, so <c>archive/../secret.jpg</c> would read <c>secret.jpg</c>
/// from outside <see cref="AzureBlobOriginalImageSourceOptions.BlobPathPrefix" />.
/// </remarks>
public sealed class AzureBlobOriginalImageSource : IOriginalImageSource
{
    private readonly Lazy<Task<BlobContainerClient>> _containerClient;
    private readonly string _blobPathPrefix;

    public AzureBlobOriginalImageSource(IOptions<AzureBlobOriginalImageSourceOptions> options)
    {
        AzureBlobOriginalImageSourceOptions blob = options.Value;
        _blobPathPrefix = blob.BlobPathPrefix.Trim('/');

        // Unlike AzureBlobDerivativeImageCache, this container is not created here: its lifecycle
        // belongs to Umbraco's own media file system, which owns writing to it. A missing container
        // means misconfiguration, not "first use" — surfaced as OpenReadAsync returning null.
        _containerClient = new Lazy<Task<BlobContainerClient>>(() => Task.FromResult(new BlobContainerClient(blob.ConnectionString, blob.ContainerName)));
    }

    public async Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default)
    {
        if (PathSegments.HasDotSegment(requestPath))
        {
            return null;
        }

        BlobContainerClient container = await _containerClient.Value.WaitAsync(cancellationToken);
        BlobClient blobClient = container.GetBlobClient(GetBlobName(requestPath));

        // Buffered rather than streamed: the interface contract requires a seekable stream (the
        // middleware reads image headers, then rewinds to re-read the full content for processing),
        // and BlobClient's streaming download (as used by AzureBlobDerivativeImageCache, which only
        // ever reads forward once) is not seekable.
        Response<BlobDownloadResult> download;
        try
        {
            download = await blobClient.DownloadContentAsync(cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        return download.Value.Content.ToStream();
    }

    /// <summary>
    /// Mirrors <c>Umbraco.StorageProviders.AzureBlob</c>'s own <c>AzureBlobFileSystem.GetBlobName</c>:
    /// blob name = the configured root prefix + the request's relative path, e.g. a request for
    /// <c>/1234/photo.jpg</c> becomes the blob <c>media/1234/photo.jpg</c> with the default prefix.
    /// </summary>
    private string GetBlobName(string requestPath) =>
        string.IsNullOrEmpty(_blobPathPrefix)
            ? requestPath.TrimStart('/', '\\')
            : $"{_blobPathPrefix}/{requestPath.TrimStart('/', '\\')}";
}
