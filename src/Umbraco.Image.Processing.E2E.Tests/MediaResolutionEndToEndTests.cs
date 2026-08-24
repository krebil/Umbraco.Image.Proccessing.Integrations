using System.Net;
using SkiaSharp;
using Xunit;

namespace Umbraco.Image.Processing.E2E.Tests;

/// <summary>
/// Production-hardening ticket 11: proves an image saved through Umbraco's <em>real</em> media pipeline
/// — running as a genuinely separate process from the standalone Service, communicating over real HTTP,
/// via the real <c>Umbraco.Image.Processing.AppHost</c> orchestration — is resolvable through the
/// standalone Service, for both local-disk media storage (today's default) and Azure Blob media storage
/// (<c>Umbraco.StorageProviders.AzureBlob</c> + the new <c>AzureBlobOriginalImageSource</c>). Unlike
/// <c>Service.Tests</c>' <c>MediaResolutionTests</c> (ticket 07), nothing here hand-writes a file at a
/// path computed to match Umbraco's scheme — Umbraco itself saves the file, in its own process, and the
/// only thing this suite controls from the outside is the HTTP requests both apps receive.
/// </summary>
public sealed class MediaResolutionEndToEndTests
{
    [Fact]
    public async Task ImageSavedByRealUmbracoOnLocalDisk_IsResolvableThroughTheSeparatelyRunningStandaloneService()
    {
        await using var harness = new UmbracoServiceGraphHarness();
        await harness.StartAsync(storageMode: "LocalDisk");

        string relativeUrl = await harness.SaveMediaAsync(FourCornerPngBytes(), "e2e-local-disk.png");
        string? mediaFolderToCleanUp = TryLocateLocalMediaFolder(relativeUrl);

        try
        {
            string signedUrl = harness.SignedResizeUrl(relativeUrl, width: 200);

            using HttpResponseMessage response = await harness.ServiceClient.GetAsync(signedUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            using SKBitmap image = SKBitmap.Decode(bytes);
            Assert.Equal(200, image.Width);
        }
        finally
        {
            // Best-effort repo-tree hygiene: LocalDisk mode's media root is the real, checked-in
            // src/Umbraco/wwwroot/media (shared with normal local dev — see AppHost.cs's comments on
            // why only the database gets isolated per run, not the media folder). Never fails the test.
            TryDeleteDirectory(mediaFolderToCleanUp);
        }
    }

    [Fact]
    public async Task ImageSavedByRealUmbracoOnAzureBlob_IsResolvableThroughTheSeparatelyRunningStandaloneService()
    {
        await using var harness = new UmbracoServiceGraphHarness();
        await harness.StartAsync(storageMode: "AzureBlob");

        string relativeUrl = await harness.SaveMediaAsync(FourCornerPngBytes(), "e2e-azure-blob.png");
        string signedUrl = harness.SignedResizeUrl(relativeUrl, width: 200);

        using HttpResponseMessage response = await harness.ServiceClient.GetAsync(signedUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(200, image.Width);
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
