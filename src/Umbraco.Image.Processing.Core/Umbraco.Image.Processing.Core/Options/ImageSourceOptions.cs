namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// One image source: a route the middleware serves images under. Where its original images come from is registered
/// separately, keyed by <see cref="RoutePrefix" /> (e.g. <c>UseAzureBlobOriginalImageSource(routePrefix, ...)</c>);
/// a source with no storage of its own falls back to the default <c>IOriginalImageSource</c>.
/// </summary>
public sealed class ImageSourceOptions
{
    /// <summary>
    /// The path prefix this source handles, e.g. <c>/media</c>. Matched on whole segments, case-insensitively;
    /// the longest matching prefix wins. <see cref="string.Empty" /> (or <c>/</c>) matches every path.
    /// </summary>
    public string? RoutePrefix { get; set; }

    /// <summary>
    /// Optional regular expression the path after <see cref="RoutePrefix" /> (without its leading <c>/</c>)
    /// must match in full, e.g. <c>^[0-9a-f]{32}/assets/[0-9a-f]{32}\.[a-z0-9]{1,10}$</c>. Requests that
    /// don't match are not served by this source, nor by any shorter prefix.
    /// </summary>
    public string? PathPattern { get; set; }
}
