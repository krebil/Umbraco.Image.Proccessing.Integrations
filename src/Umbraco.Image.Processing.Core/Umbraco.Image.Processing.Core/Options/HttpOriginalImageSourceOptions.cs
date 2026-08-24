namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// Connection settings for <c>HttpOriginalImageSource</c>, bound from <c>ImageProcessing:Proxy</c> — a
/// section distinct from <see cref="ImageProcessingOptions.ExternalBaseUrl" />/<c>Standalone:BaseUrl</c>,
/// which point the opposite direction (Umbraco → Service, for generated <c>&lt;img&gt;</c> URLs and the
/// redirect fallback). This points Service → Umbraco, for fetching raw originals back.
/// </summary>
public sealed class HttpOriginalImageSourceOptions
{
    /// <summary>
    /// Umbraco's own base URL, reachable from the Service (e.g. an internal service DNS name or
    /// container network address) — not necessarily the same host the public <c>Standalone:BaseUrl</c>
    /// points at in the opposite direction.
    /// </summary>
    public string UmbracoBaseUrl { get; set; } = string.Empty;
}
