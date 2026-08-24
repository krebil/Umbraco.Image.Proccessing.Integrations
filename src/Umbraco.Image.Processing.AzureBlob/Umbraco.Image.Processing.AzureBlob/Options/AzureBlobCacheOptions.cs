namespace Umbraco.Image.Processing.AzureBlob.Options;

/// <summary>
/// Connection settings for <c>AzureBlobDerivativeImageCache</c>, separate from the
/// processor-agnostic <c>ImageProcessingOptions</c> (which still supplies <c>CacheControlMaxAge</c>
/// for TTL eviction — this options type only carries the Blob-specific connection details).
/// </summary>
public sealed class AzureBlobCacheOptions
{
    /// <summary>
    /// A Blob Storage connection string, e.g. an Azurite/emulator connection string in development or
    /// a real storage account's connection string in production. Can be the same connection
    /// string/storage account <c>AzureBlobOriginalImageSourceOptions</c> uses — the two are kept as
    /// separate containers (see <see cref="ContainerName" />) within that one account, not merged into
    /// one container, so this cache's own lifecycle (auto-created, freely cleared) never touches
    /// Umbraco's real media.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The container derivative output is cached into. Created automatically on first use if it
    /// doesn't already exist. Deliberately its own container, separate from wherever Umbraco's media
    /// originals live — <c>ClearAsync</c>/<c>EvictExpiredAsync</c> enumerate and delete everything in
    /// this container, which would be unsafe if it were shared with Umbraco's own media.
    /// </summary>
    public string ContainerName { get; set; } = "image-derivative-cache";
}
