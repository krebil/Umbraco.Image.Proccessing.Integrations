namespace Umbraco.Image.Processing.Core.Storage;

/// <summary>
/// An image source's original-image storage: reads the unprocessed file for the request path after the
/// source's route prefix. Registered per route prefix (local disk, Azure Blob, or an HTTP proxy to Umbraco),
/// with the unkeyed registration as the default for sources without storage of their own.
/// </summary>
public interface IOriginalImageSource
{
    /// <summary>
    /// Opens a seekable, readable stream for <paramref name="requestPath" />, or <see langword="null" />
    /// if it doesn't resolve to an existing file.
    /// </summary>
    Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default);
}
