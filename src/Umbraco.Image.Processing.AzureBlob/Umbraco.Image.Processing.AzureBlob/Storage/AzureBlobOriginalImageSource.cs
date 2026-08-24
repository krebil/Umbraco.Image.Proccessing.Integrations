using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.AzureBlob.Options;
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
/// Blob names have no directory-traversal risk the way local-disk paths do — Azure Blob Storage has no
/// real filesystem hierarchy; a "/" in a blob name is just a character used for virtual-folder display,
/// not path resolution, so a name containing "../" segments still only ever addresses one exact blob,
/// never an ancestor. <see cref="LocalDiskOriginalImageSource" />'s root-escape check has no equivalent
/// here for that reason.
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
