using System.Net;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.Storage;

namespace ImageProcessingDemo;

/// <summary>
/// A plain, code-first demo page for the in-process sample — not Umbraco content. It exists so a
/// freshly installed site has something to look at beyond curling <c>/media/...</c> directly: the
/// sample image rendered at several sizes/commands through <see cref="IImageUrlGenerator" />, plus a
/// button that clears the derivative cache via <see cref="IDerivativeImageCache" />.
/// </summary>
internal static class ImageProcessingDemoEndpoints
{
    private const string RoutePath = "/image-processing-demo";
    private const string SampleImagePath = "/media/sample.jpg";

    public static void MapImageProcessingDemo(this WebApplication app)
    {
        // Mapped on "/" too — the site has no authored content yet, so the root would otherwise be
        // blank; this gives a freshly installed site something to look at immediately.
        app.MapGet("/", (IImageUrlGenerator urlGenerator, HttpRequest request) =>
            Results.Content(RenderPage(urlGenerator, cleared: request.Query.ContainsKey("cleared")), "text/html"));

        app.MapGet(RoutePath, (IImageUrlGenerator urlGenerator, HttpRequest request) =>
            Results.Content(RenderPage(urlGenerator, cleared: request.Query.ContainsKey("cleared")), "text/html"));

        app.MapPost($"{RoutePath}/clear-cache", async (IDerivativeImageCache cache) =>
        {
            await cache.ClearAsync();
            return Results.Redirect("/?cleared=1");
        });
    }

    private static string RenderPage(IImageUrlGenerator urlGenerator, bool cleared)
    {
        (string Caption, string Url)[] variants =
        [
            ("Original (no processing commands)", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath))),
            ("Width 800", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath) { Width = 800 })),
            ("Width 400", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath) { Width = 400 })),
            ("Width 200", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath) { Width = 200 })),
            ("Width 400, format webp", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath) { Width = 400, Format = "webp" })),
            ("300x300 centre crop (cc)", Url(urlGenerator, new ImageUrlGenerationOptions(SampleImagePath)
            {
                Width = 300,
                Height = 300,
                Crop = new ImageUrlGenerationOptions.CropCoordinates(0.25m, 0.25m, 0.25m, 0.25m),
            })),
        ];

        string clearedBanner = cleared
            ? "<p class=\"banner\">Derivative cache cleared.</p>"
            : string.Empty;

        string figures = string.Join(
            Environment.NewLine,
            variants.Select(v => $"""
                <figure>
                    <img src="{WebUtility.HtmlEncode(v.Url)}" alt="{WebUtility.HtmlEncode(v.Caption)}" loading="lazy" />
                    <figcaption>{WebUtility.HtmlEncode(v.Caption)}</figcaption>
                </figure>
                """));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>Image processing demo</title>
                <style>
                    body { font-family: system-ui, sans-serif; margin: 2rem; color: #1a1a1a; }
                    .gallery { display: flex; flex-wrap: wrap; gap: 1.5rem; }
                    figure { margin: 0; padding: 0.75rem; border: 1px solid #ddd; border-radius: 6px; }
                    figure img { display: block; max-width: 320px; max-height: 320px; height: auto; }
                    figcaption { margin-top: 0.5rem; font-size: 0.85rem; color: #555; }
                    .banner { color: #0a6b2d; font-weight: 600; }
                    button { font: inherit; padding: 0.5rem 1rem; cursor: pointer; }
                </style>
            </head>
            <body>
                <h1>Image processing demo</h1>
                <p>Sample image served through the pluggable image-processing middleware, at a few commands.</p>
                {{clearedBanner}}
                <form method="post" action="{{RoutePath}}/clear-cache">
                    <button type="submit">Clear derivative cache</button>
                </form>
                <div class="gallery">
                    {{figures}}
                </div>
            </body>
            </html>
            """;
    }

    private static string Url(IImageUrlGenerator urlGenerator, ImageUrlGenerationOptions options) =>
        urlGenerator.GetImageUrl(options) ?? options.ImageUrl ?? string.Empty;
}
