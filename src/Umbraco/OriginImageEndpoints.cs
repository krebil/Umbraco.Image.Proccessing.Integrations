using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

namespace ImageProcessingDemo;

/// <summary>
/// Serves raw (unprocessed) originals to a standalone Service configured with
/// <c>HttpOriginalImageSource</c> — for deployments where Umbraco and the Service are separate
/// processes with no shared disk/volume and Umbraco's media isn't Blob-backed either
/// (production-hardening ticket 12). Mounted at <see cref="HttpOriginalImageSource.OriginRoutePrefix" />,
/// deliberately outside <c>ImageProcessing:RoutePrefix</c> — the Standalone-mode redirect middleware
/// below only matches requests under that prefix, so this route never enters its matching logic and
/// can't loop a Service request straight back to itself. Resolves the same <see cref="IOriginalImageSource" />
/// (local disk or Blob, depending on <c>Storage:Mode</c>) and <see cref="IHmacSigner" /> that
/// <c>AddImageProcessing()</c>/<c>AddUmbracoImageProcessing()</c> above already register — no separate
/// Umbraco media-API access needed, and no bulk-download exposure since the HMAC guard is the same one
/// the rest of this app's image requests are validated against.
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
            IOriginalImageSource originalImageSource,
            IHmacSigner hmacSigner) =>
        {
            if (!hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            string relativePath = "/" + ((string?)context.GetRouteValue("path") ?? string.Empty);
            Stream? source = await originalImageSource.OpenReadAsync(relativePath, context.RequestAborted);
            return source is null
                ? Results.NotFound()
                : Results.Stream(source, "application/octet-stream");
        });
    }
}
