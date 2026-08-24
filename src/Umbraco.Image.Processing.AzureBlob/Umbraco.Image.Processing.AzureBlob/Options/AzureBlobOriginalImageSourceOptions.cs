namespace Umbraco.Image.Processing.AzureBlob.Options;

/// <summary>
/// Connection settings for <c>AzureBlobOriginalImageSource</c>, separate from
/// <see cref="AzureBlobCacheOptions" /> — the original-image container and the derivative-cache
/// container are different concerns (one holds Umbraco's own media, the other holds this product's
/// processed output) and must stay independently configurable even when both point at the same
/// storage account.
/// </summary>
public sealed class AzureBlobOriginalImageSourceOptions
{
    /// <summary>
    /// A Blob Storage connection string, e.g. an Azurite/emulator connection string in development or
    /// a real storage account's connection string in production. Should match whatever connection
    /// string Umbraco's own media file system (e.g. <c>Umbraco.StorageProviders.AzureBlob</c>) is
    /// configured with, since this reads the same media Umbraco writes.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The container Umbraco's media file system writes originals into. Not created automatically —
    /// unlike the derivative cache, this container's lifecycle belongs to Umbraco's media file system,
    /// not to this reader. Must match whatever <c>ContainerName</c> Umbraco's own Blob media file
    /// system (e.g. <c>Umbraco.StorageProviders.AzureBlob</c>'s <c>AzureBlobFileSystemOptions</c>) is
    /// configured with.
    /// </summary>
    public string ContainerName { get; set; } = "media";

    /// <summary>
    /// The path prefix within <see cref="ContainerName" /> that Umbraco's media file system stores
    /// blobs under, prepended to every request's relative path to form the actual blob name — e.g. a
    /// request for <c>/1234/photo.jpg</c> resolves to the blob <c>media/1234/photo.jpg</c> with the
    /// default prefix. Confirmed against <c>Umbraco.StorageProviders.AzureBlob</c>'s
    /// <c>AzureBlobFileSystem.GetBlobName</c>: when its <c>ContainerRootPath</c> is left unset (the
    /// common case), it defaults to the media file system's <c>VirtualPath</c>, itself defaulted from
    /// <c>GlobalSettings.UmbracoMediaPath</c> (<c>~/media</c>) — so "media" is the real-world default,
    /// not an assumption. Set to match whichever of <c>ContainerRootPath</c>/<c>VirtualPath</c> the
    /// Umbraco side actually resolves to if either was overridden.
    /// </summary>
    public string BlobPathPrefix { get; set; } = "media";
}
