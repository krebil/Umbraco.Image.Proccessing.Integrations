using Umbraco.Image.Processing.Core.Routing;

namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// Connection settings for <c>HttpOriginalImageSource</c>, bound per source (in the reference Service from
/// <c>ImageProcessing:Sources:N:Storage:HttpProxy</c>) — a section distinct from <see cref="ImageProcessingOptions.ExternalBaseUrl" />/<c>Standalone:BaseUrl</c>,
/// which point the opposite direction (Umbraco → Service, for generated <c>&lt;img&gt;</c> URLs and the
/// redirect fallback). This points Service → Umbraco, for fetching original images back.
/// </summary>
public sealed class HttpOriginalImageSourceOptions
{
    /// <summary>
    /// Umbraco's own base URL, reachable from the Service (e.g. an internal service DNS name or
    /// container network address) — not necessarily the same host the public <c>Standalone:BaseUrl</c>
    /// points at in the opposite direction.
    /// </summary>
    public string UmbracoBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The route prefix of the source this storage serves, sent to Umbraco's origin endpoint ahead of the
    /// relative path (<c>/__image-origin{RoutePrefix}/{path}</c>) so Umbraco resolves the same source. The
    /// per-prefix <c>UseHttpOriginalImageSource(routePrefix, …)</c> overload sets it to the prefix it
    /// registers for; the default source serves <c>/media</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = ImageSourceMatcher.DefaultRoutePrefix;
}
