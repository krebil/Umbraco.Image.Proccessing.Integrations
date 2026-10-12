using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;
using File = System.IO.File;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// One Service instance serving several sources, each with its own storage, selected by route prefix
/// and gated by an optional path pattern.
/// </summary>
public sealed class MultiSourceTests : IDisposable
{
    private const string Hex32 = "0123456789abcdef0123456789abcdef";
    private const string ArchivePattern = @"^[0-9a-f]{32}/assets/[0-9a-f]{32}\.[a-z0-9]{1,10}$";

    private readonly string _mediaRoot = NewTempDirectory("service-multisource-media-");
    private readonly string _archiveRoot = NewTempDirectory("service-multisource-archive-");
    private readonly string _cacheRoot = NewTempDirectory("service-multisource-cache-");
    private readonly byte[] _hmacSecretKey = RandomNumberGenerator.GetBytes(32);

    public void Dispose()
    {
        foreach (string directory in new[] { _mediaRoot, _archiveRoot, _cacheRoot })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EachPrefix_ReadsFromItsOwnStorage_AndDerivativesDoNotCollide()
    {
        // Same relative path under both prefixes, different originals: a cache key without the prefix
        // would hand the second request the first one's derivative.
        string relative = $"{Hex32}/assets/{Hex32}.png";
        await WriteAsync(_mediaRoot, relative, TestImages.SolidColorPngBytes(40, 20));
        await WriteAsync(_archiveRoot, relative, TestImages.SolidColorPngBytes(20, 40));

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using SKBitmap media = await GetImageAsync(client, urls.Signed($"/media/{relative}", ("width", "10")));
        using SKBitmap archive = await GetImageAsync(client, urls.Signed($"/-/archive/{relative}", ("width", "10")));

        Assert.Equal((10, 5), (media.Width, media.Height));
        Assert.Equal((10, 20), (archive.Width, archive.Height));
    }

    [Fact]
    public async Task PathOutsideTheSourcePattern_IsNotServed()
    {
        await WriteAsync(_archiveRoot, $"{Hex32}/pages/{Hex32}.png", TestImages.FourCornerPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/-/archive/{Hex32}/pages/{Hex32}.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveRequest_SignedOverTheFullPath_IsAccepted_AndUnsignedResizeIsRejected()
    {
        string relative = $"{Hex32}/assets/{Hex32}.png";
        await WriteAsync(_archiveRoot, relative, TestImages.SolidColorPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage signed = await client.GetAsync(urls.Signed($"/-/archive/{relative}", ("width", "50")));
        using HttpResponseMessage unsigned = await client.GetAsync(SignedRequestUrlBuilder.Unsigned($"/-/archive/{relative}", ("width", "50")));
        using HttpResponseMessage signedForMedia = await client.GetAsync(
            urls.Signed($"/media/{relative}", ("width", "50")).Replace("/media/", "/-/archive/", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.OK, signed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsigned.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, signedForMedia.StatusCode);
    }

    [Fact]
    public void NoSourcesConfigured_FailsAtStartup()
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.CreateWithoutAppSettings(new Dictionary<string, string?>
        {
            ["ImageProcessing:Processor"] = "ImageFlow",
        });

        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("ImageProcessing:Sources", ex.Message);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:0:RoutePrefix"] = "/media",
            ["ImageProcessing:Sources:0:Storage:Mode"] = "LocalDisk",
            ["ImageProcessing:Sources:0:Storage:LocalDisk:RootPath"] = _mediaRoot,
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/-/archive",
            ["ImageProcessing:Sources:1:PathPattern"] = ArchivePattern,
            ["ImageProcessing:Sources:1:Storage:Mode"] = "LocalDisk",
            ["ImageProcessing:Sources:1:Storage:LocalDisk:RootPath"] = _archiveRoot,
            ["ImageProcessing:DerivativeCacheRootPath"] = _cacheRoot,
            ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(_hmacSecretKey),
        });

    private static async Task<SKBitmap> GetImageAsync(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
    }

    private static async Task WriteAsync(string root, string relativePath, byte[] bytes)
    {
        string fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);
    }

    private static string NewTempDirectory(string prefix) => Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
}
