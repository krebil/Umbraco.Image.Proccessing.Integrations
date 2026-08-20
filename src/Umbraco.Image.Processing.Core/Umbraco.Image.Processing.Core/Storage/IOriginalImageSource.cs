namespace Umbraco.Image.Processing.Core.Storage;

/// <summary>
/// Reads original media by request path. This POC's only implementation is local disk.
/// </summary>
public interface IOriginalImageSource
{
    /// <summary>
    /// Opens a seekable, readable stream for <paramref name="requestPath" />, or <see langword="null" />
    /// if it doesn't resolve to an existing file.
    /// </summary>
    Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default);
}
