using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Storage;

/// <summary>
/// Caches processed (derivative) output so repeat requests for the same command set skip
/// re-processing.
/// </summary>
public interface IDerivativeImageCache
{
    /// <summary>
    /// Returns the cached content for <paramref name="cacheKey" />, or <see langword="null" /> if
    /// no entry exists or the entry is older than <see cref="ImageProcessingOptions.CacheControlMaxAge" />
    /// (ADR-0007) — an expired entry is treated as absent here without waiting for an eviction pass.
    /// </summary>
    Task<Stream?> TryOpenReadAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task WriteAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every cached derivative, forcing the next request for each to be reprocessed.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically removes every entry older than <see cref="ImageProcessingOptions.CacheControlMaxAge" />
    /// (ADR-0007). TTL-only, no LRU/max-size: a derivative cached under a stale <c>v</c> cache-buster
    /// value is never requested again by any live URL, so eviction timing never affects correctness —
    /// it only reclaims space.
    /// </summary>
    Task EvictExpiredAsync(CancellationToken cancellationToken = default);
}
