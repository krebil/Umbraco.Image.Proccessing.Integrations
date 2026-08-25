using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Umbraco.Cms.Core.IO.MediaPathSchemes;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;
using Xunit;
using File = System.IO.File;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// Proves the most common real-world path end to end: a media file physically stored the way
/// Umbraco's own media pipeline stores it — via its real, default <see cref="IMediaPathScheme" />
/// implementation, <see cref="UniqueMediaPathScheme" /> — is resolvable through the standalone
/// Service's HTTP pipeline, at the exact URL <see cref="ImageProcessingUrlGenerator" /> (the same
/// class Umbraco calls in-process) would hand back for it. Both sides are real production classes,
/// not stand-ins: neither one reimplements or guesses the media folder shape independently — Core's
/// <c>LocalDiskOriginalImageSource</c> just combines <c>OriginalsRootPath</c> with whatever relative
/// path it's given, and <see cref="ImageProcessingUrlGenerator" /> passes Umbraco's own computed
/// <see cref="ImageUrlGenerationOptions.ImageUrl" /> straight through unchanged (see ADR-0006 /
/// production-hardening ticket 07's "wiring regression" concern) — so the only thing actually under
/// test is whether the two sides' independently-configured root paths agree, which is exactly the
/// class of bug a config typo (e.g. Service's <c>appsettings.json</c> <c>OriginalsRootPath</c>) would
/// cause and unit tests at either seam alone can't catch.
/// </summary>
public sealed class MediaResolutionTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "service-tests-media-" + Guid.NewGuid().ToString("N"));
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "service-tests-cache-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _hmacSecretKey = RandomNumberGenerator.GetBytes(32);

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }

        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ImageSavedTheWayUmbracoSavesIt_IsResolvableThroughTheStandaloneService()
    {
        // Arrange: compute the relative path via Umbraco's actual media-path algorithm — not a
        // hand-typed guess — and write a real source image there, simulating what Umbraco's media
        // pipeline does on a real upload.
        string relativePath = new UniqueMediaPathScheme().GetFilePath(fileManager: null!, Guid.NewGuid(), Guid.NewGuid(), "photo.jpg");
        string fullPath = Path.Combine(_mediaRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, TestImages.FourCornerPngBytes());

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageProcessing:OriginalsRootPath"] = _mediaRoot,
                ["ImageProcessing:DerivativeCacheRootPath"] = _cacheRoot,
                ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(_hmacSecretKey),
            })));

        // The URL Umbraco's own media pipeline would hand IImageUrlGenerator for this item.
        // ImageProcessingUrlGenerator only appends query params onto it — it never reshapes the path.
        var urlGeneratorOptions = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = _hmacSecretKey });
        var urlGenerator = new ImageProcessingUrlGenerator(urlGeneratorOptions, new HmacSigner(urlGeneratorOptions));
        string? url = urlGenerator.GetImageUrl(new ImageUrlGenerationOptions("/media/" + relativePath) { Width = 200 });
        Assert.NotNull(url);
        Assert.Contains("hmac=", url);

        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(url);

        // Assert: request succeeded (proves OriginalsRootPath + the Umbraco-shaped relative path
        // resolve to the file), and the resize actually ran against it (proves it's the real file,
        // not e.g. a 200 from some unrelated fallback).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(200, image.Width);
    }

    [Fact]
    public async Task ImageAtWrongRelativePath_IsNotResolvable()
    {
        // Sanity check for the fact above: an otherwise-identical request against a relative path
        // Umbraco did NOT compute for this file must fail — proving the prior test's success is
        // actually contingent on path agreement, not on the Service resolving anything under the root.
        string relativePath = new UniqueMediaPathScheme().GetFilePath(fileManager: null!, Guid.NewGuid(), Guid.NewGuid(), "photo.jpg");
        string fullPath = Path.Combine(_mediaRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, TestImages.FourCornerPngBytes());

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageProcessing:OriginalsRootPath"] = _mediaRoot,
                ["ImageProcessing:DerivativeCacheRootPath"] = _cacheRoot,
                ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(_hmacSecretKey),
            })));

        var urlGeneratorOptions = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = _hmacSecretKey });
        var urlGenerator = new ImageProcessingUrlGenerator(urlGeneratorOptions, new HmacSigner(urlGeneratorOptions));
        string? url = urlGenerator.GetImageUrl(new ImageUrlGenerationOptions("/media/not-the-real-path/photo.jpg") { Width = 200 });
        Assert.NotNull(url);

        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
