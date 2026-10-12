namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// Settings for a source's <c>LocalDiskOriginalImageSource</c>.
/// </summary>
public sealed class LocalDiskOriginalImageSourceOptions
{
    /// <summary>
    /// Physical root directory the source's original images are read from; the request path after the source's
    /// route prefix is resolved under it.
    /// </summary>
    public string RootPath { get; set; } = "wwwroot/media";
}
