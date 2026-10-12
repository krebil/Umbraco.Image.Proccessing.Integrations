using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

namespace ImageProcessingDemo;

/// <summary>
/// Serves raw (unprocessed) original images to a standalone Service configured with
/// <c>HttpOriginalImageSource</c> — for deployments where Umbraco and the Service are separate
/// processes with no shared disk/volume and Umbraco's media isn't Blob-backed either
/// (production-hardening ticket 12). Mounted at <see cref="HttpOriginalImageSource.OriginRoutePrefix" />,
/// deliberately outside every <c>ImageProcessing:Sources</c> route prefix — the Standalone-mode redirect
/// middleware below only matches requests under those prefixes, so this route never enters its matching logic and
/// can't loop a Service request straight back to itself. That holds because a root source (<c>/</c>), which
/// would take this path too and turn every origin fetch into a redirect back to the Service, is refused at
/// start-up by the matcher and the Service alike. The Service sends the source's full path after the
/// origin prefix (<c>/__image-origin/media/1234/photo.jpg</c>); the same <see cref="ImageSourceMatcher" />
/// picks the source, so its path gate applies here too, and the <see cref="IOriginalImageSource" />
/// registered for that source (or the default: local disk or Blob, depending on <c>MediaStorage:Mode</c>)
/// reads it. The <see cref="IHmacSigner" /> is the one <c>AddImageProcessing()</c> already registers — no
/// separate Umbraco media-API access needed, and no bulk-download exposure since the HMAC guard is the
/// same one the rest of this app's image requests are validated against.
/// </summary>
/// <remarks>
/// Mounted unconditionally, not gated on <c>ImageProcessing:Mode</c>: it's only ever actually called
/// when the Service is separately configured with <c>UseHttpOriginalImageSource</c>, but registering it
/// regardless costs nothing (no new unauthenticated surface beyond what the rest of the app already
/// exposes) and avoids a branch here that would need to stay in sync with a config value that lives on
/// the Service's side, which this app has no visibility into.
/// </remarks>
internal static class OriginImageEndpoints
{
    public static void MapOriginImageEndpoints(this WebApplication app)
    {
        app.MapGet($"{HttpOriginalImageSource.OriginRoutePrefix}/{{**path}}", async (
            HttpContext context,
            ImageSourceMatcher sourceMatcher,
            IHmacSigner hmacSigner) =>
        {
            if (!hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            var sourcePath = new PathString("/" + ((string?)context.GetRouteValue("path") ?? string.Empty));
            if (!sourceMatcher.TryMatch(sourcePath, out ImageSourceMatch? match))
            {
                return Results.NotFound();
            }

            IOriginalImageSource originalImageSource = context.RequestServices.GetOriginalImageSource(match.RoutePrefix);
            Stream? source = await originalImageSource.OpenReadAsync(match.RelativePath, context.RequestAborted);
            return source is null
                ? Results.NotFound()
                : Results.Stream(source, "application/octet-stream");
        });
    }
}
