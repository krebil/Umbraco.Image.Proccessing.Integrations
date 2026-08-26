using System.Net;
using SkiaSharp;
using Xunit;

namespace Umbraco.Image.Processing.E2E.Tests;

/// <summary>
/// Production-hardening ticket 12's redirect-loop regression test. Proves the deployment shape neither
/// ticket 07's local-disk resolution nor ticket 11's Blob resolution covers: Umbraco's media stays on
/// plain local disk, Umbraco and the Service are genuinely separate processes with no shared
/// disk/volume, and the Service has to fetch the raw original from Umbraco itself over HTTP
/// (<c>HttpOriginalImageSource</c>) without that request bouncing back through Umbraco's own
/// Standalone-mode redirect middleware. Per ADR-0008, this can only be proven with real, separately
/// listening processes communicating over a real network round trip — a same-process
/// <c>WebApplicationFactory</c>/<c>TestServer</c> call wouldn't exercise the loop this test exists to
/// rule out.
/// </summary>
public sealed class HttpProxyOriginalImageSourceEndToEndTests
{
    [Fact]
    public async Task ServiceResolvesOriginalOverHttp_WithoutLoopingBackThroughUmbracosRedirectMiddleware()
    {
        await using var harness = new UmbracoServiceGraphHarness();
        await harness.StartAsync(storageMode: "HttpProxy");

        string relativeUrl = await harness.SaveMediaAsync(FourCornerPngBytes(), "e2e-http-proxy.png");
        string? mediaFolderToCleanUp = TryLocateLocalMediaFolder(relativeUrl);

        try
        {
            // Sanity check: Standalone mode's redirect middleware really is active for the *processed*
            // media route — a plain pass-through request to Umbraco's own /media/... must 302 to the
            // Service. If this assertion ever fails, the rest of this test would be proving nothing: it
            // would mean Umbraco silently fell back to InProcess mode rather than genuinely exercising
            // the cross-deployment shape ticket 12 is about. AllowAutoRedirect is disabled here
            // specifically so the 302 itself is observable instead of being silently followed to a 200
            // from the Service.
            using var noAutoRedirectHandler = new HttpClientHandler { AllowAutoRedirect = false };
            using var noAutoRedirectUmbracoClient = new HttpClient(noAutoRedirectHandler) { BaseAddress = harness.UmbracoClient.BaseAddress };
            using HttpResponseMessage passthroughResponse = await noAutoRedirectUmbracoClient.GetAsync(relativeUrl);
            Assert.Equal(HttpStatusCode.Redirect, passthroughResponse.StatusCode);

            // The raw-original route itself, hit directly against Umbraco: returns the bytes straight
            // away, not a redirect — proving it never enters the matching logic above (it's outside
            // RoutePrefix entirely, see HttpOriginalImageSource's own remarks on why that's what breaks
            // the loop).
            string originUrl = UmbracoServiceGraphHarness.SignedOriginUrl(relativeUrl);
            using HttpResponseMessage originResponse = await noAutoRedirectUmbracoClient.GetAsync(originUrl);
            Assert.Equal(HttpStatusCode.OK, originResponse.StatusCode);

            // End to end: a resize request against the Service succeeds — proving
            // HttpOriginalImageSource's whole round trip (Service -> Umbraco's raw-original endpoint ->
            // back to the Service) actually completes, and completes with the right bytes, rather than
            // hanging/erroring on a loop.
            string signedResizeUrl = UmbracoServiceGraphHarness.SignedResizeUrl(relativeUrl, width: 200);
            using HttpResponseMessage response = await harness.ServiceClient.GetAsync(signedResizeUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            using SKBitmap image = SKBitmap.Decode(bytes);
            Assert.Equal(200, image.Width);
        }
        finally
        {
            // Best-effort repo-tree hygiene: HttpProxy mode still saves through Umbraco's real,
            // checked-in src/Umbraco/wwwroot/media (same as LocalDisk mode — see
            // MediaResolutionEndToEndTests and AppHost.cs's comments on why only the database gets
            // isolated per run, not the media folder). Never fails the test.
            TryDeleteDirectory(mediaFolderToCleanUp);
        }
    }

    private static byte[] FourCornerPngBytes()
    {
        using var bitmap = new SKBitmap(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0));
        bitmap.SetPixel(1, 0, new SKColor(0, 255, 0));
        bitmap.SetPixel(0, 1, new SKColor(0, 0, 255));
        bitmap.SetPixel(1, 1, new SKColor(255, 255, 0));

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Maps a saved media item's URL (e.g. <c>/media/1234/photo.jpg</c>) back to the physical folder
    /// under the checked-in <c>src/Umbraco/wwwroot/media</c>, walking up from this test assembly's own
    /// build output — the two projects' relative layout in the repo is fixed, so this is stable, but it
    /// is inherently best-effort: returns null (skip cleanup) rather than throw if anything's off.
    /// Duplicated from <see cref="MediaResolutionEndToEndTests" /> rather than shared: both are small,
    /// self-contained, best-effort test helpers, not product code.
    /// </summary>
    private static string? TryLocateLocalMediaFolder(string relativeUrl)
    {
        try
        {
            string mediaSegment = relativeUrl.TrimStart('/'); // "media/1234/photo.jpg"
            string firstFolder = mediaSegment.Split('/') is [_, var folder, ..] ? folder : string.Empty;
            if (string.IsNullOrEmpty(firstFolder))
            {
                return null;
            }

            string repoSrcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string candidate = Path.Combine(repoSrcDir, "Umbraco", "wwwroot", "media", firstFolder);
            return Directory.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
