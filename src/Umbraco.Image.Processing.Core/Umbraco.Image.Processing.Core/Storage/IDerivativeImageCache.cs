namespace Umbraco.Image.Processing.Core.Storage;

/// <summary>
/// Caches processed (derivative) output so repeat requests for the same command set skip
/// re-processing. This POC's only implementation is local disk.
/// </summary>
public interface IDerivativeImageCache
{
    Task<Stream?> TryOpenReadAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task WriteAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default);
}
