using System.Net;
using SkiaSharp;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;

namespace Umbraco.Image.Processing.E2E.Tests;

/// <summary>
/// Two sources across the Umbraco/Service boundary: Umbraco's redirect sends a request for the second
/// source (<c>/-/archive</c>, gated by a path pattern) to the separately running Service, which serves it
/// from that source's own storage, and a path the pattern does not admit is not redirected at all. Per
/// ADR-0008 this needs both real processes: the redirect, the shared source list and the Service's own
/// matching are three independently configured things that only agree if the wiring does.
/// </summary>
public sealed class MultiSourceEndToEndTests
{
    private const string Hex32 = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task UmbracoRedirectsASecondSourceRequestToTheService_WhichServesIt_AndDoesNotRedirectAPatternMiss()
    {
        string archiveRoot = Path.Combine(Path.GetTempPath(), "e2e-second-source-" + Guid.NewGuid().ToString("N"));
        string asset = $"{Hex32}/assets/{Hex32}.png";
        string page = $"{Hex32}/pages/{Hex32}.png";
        Write(Path.Combine(archiveRoot, asset), TestImages.SolidColorPngBytes(40, 20));
        Write(Path.Combine(archiveRoot, page), TestImages.SolidColorPngBytes(40, 20));

        try
        {
            await using var harness = new UmbracoServiceGraphHarness();
            await harness.StartAsync(secondSourceRootPath: archiveRoot);

            // AllowAutoRedirect is off so the 302 itself is observable instead of being followed silently.
            using var noAutoRedirectHandler = new HttpClientHandler { AllowAutoRedirect = false };
            using var umbraco = new HttpClient(noAutoRedirectHandler) { BaseAddress = harness.UmbracoClient.BaseAddress };

            string signedUrl = UmbracoServiceGraphHarness.SignedResizeUrl($"/-/archive/{asset}", width: 20);
            using HttpResponseMessage redirect = await umbraco.GetAsync(signedUrl);
            Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
            Uri target = Assert.IsType<Uri>(redirect.Headers.Location);
            Assert.Equal(signedUrl, target.PathAndQuery);
            Assert.NotEqual(umbraco.BaseAddress!.Authority, target.Authority);

            // Followed literally, the redirect lands on the Service, which serves the second source's own file.
            using var anyHost = new HttpClient();
            using HttpResponseMessage served = await anyHost.GetAsync(target);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            using SKBitmap image = SKBitmap.Decode(await served.Content.ReadAsByteArrayAsync());
            Assert.Equal((20, 10), (image.Width, image.Height));

            // Outside the pattern: Umbraco answers itself (no redirect), and the Service would not serve it either.
            string missUrl = UmbracoServiceGraphHarness.SignedResizeUrl($"/-/archive/{page}", width: 20);
            using HttpResponseMessage miss = await umbraco.GetAsync(missUrl);
            Assert.NotEqual(HttpStatusCode.Redirect, miss.StatusCode);
            using HttpResponseMessage missOnService = await harness.ServiceClient.GetAsync(missUrl);
            Assert.Equal(HttpStatusCode.NotFound, missOnService.StatusCode);
        }
        finally
        {
            Directory.Delete(archiveRoot, recursive: true);
        }
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
