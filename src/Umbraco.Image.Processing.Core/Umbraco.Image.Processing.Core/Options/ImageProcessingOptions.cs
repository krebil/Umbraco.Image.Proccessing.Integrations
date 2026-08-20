namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// Configuration shared by the middleware, command parser, URL generator, and dimension extractor,
/// regardless of which <c>IImageProcessor</c> is active.
/// </summary>
public sealed class ImageProcessingOptions
{
    /// <summary>
    /// The path prefix the middleware handles requests under. Set to <see cref="string.Empty" /> for
    /// standalone mode, where the whole app is the image service.
    /// </summary>
    public string RoutePrefix { get; set; } = "/media";

    /// <summary>
    /// Physical root directory original media is read from.
    /// </summary>
    public string OriginalsRootPath { get; set; } = "wwwroot/media";

    /// <summary>
    /// Physical root directory derivative (processed) output is cached to. Derivative caching is
    /// disabled when set to <see langword="null" /> or empty.
    /// </summary>
    public string? DerivativeCacheRootPath { get; set; } = "App_Data/image-cache";

    /// <summary>
    /// Requested widths at or above this value are dropped (mirrors the standalone-service plan's
    /// <c>OnParseCommandsAsync</c> clamping).
    /// </summary>
    public int MaxWidth { get; set; } = 5000;

    /// <summary>
    /// Requested heights at or above this value are dropped.
    /// </summary>
    public int MaxHeight { get; set; } = 5000;

    /// <summary>
    /// Encode quality used when the request omits <c>quality</c>.
    /// </summary>
    public int DefaultQuality { get; set; } = 80;

    /// <summary>
    /// The HMAC signing secret. Signing/verification is disabled when left <see langword="null" /> or empty.
    /// </summary>
    public byte[]? HmacSecretKey { get; set; }

    public TimeSpan BrowserCacheMaxAge { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan CacheControlMaxAge { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Output formats <c>format=</c> is allowed to request.
    /// </summary>
    public HashSet<string> SupportedOutputFormats { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "gif", "bmp", "webp",
    };

    /// <summary>
    /// File extensions the middleware treats as image requests.
    /// </summary>
    public HashSet<string> SupportedRequestExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff",
    };
}
