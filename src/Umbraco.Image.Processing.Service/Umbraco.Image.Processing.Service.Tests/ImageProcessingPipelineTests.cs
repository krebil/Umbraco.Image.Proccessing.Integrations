using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;
using File = System.IO.File;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// HTTP-level pipeline coverage for the standalone Service (production-hardening ticket 07):
/// pass-through, resize, format conversion, <c>cc</c> crop, and HMAC accept/tampered/unsigned — all at
/// the status-code/header/content-type level. Pixel correctness is the parity suite's job (ticket 06);
/// <see cref="MediaResolutionTests" /> separately covers the Umbraco-media-path-agreement concern. These
/// tests exist to catch middleware wiring/registration regressions unit tests at the processor/cache
/// seam can't see.
/// </summary>
public sealed class ImageProcessingPipelineTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "service-pipeline-tests-media-" + Guid.NewGuid().ToString("N"));
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "service-pipeline-tests-cache-" + Guid.NewGuid().ToString("N"));
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
    public async Task PassThroughRequest_ReturnsOriginalImageUnmodified()
    {
        byte[] original = TestImages.FourCornerPngBytes();
        await WriteSourceAsync("pass-through.png", original);

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(urls.Signed("/media/pass-through.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(original, bytes);
    }

    [Fact]
    public async Task ResizeRequest_ReturnsExpectedDimensions()
    {
        await WriteSourceAsync("resize.png", TestImages.SolidColorPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(urls.Signed("/media/resize.png", ("width", "40")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(40, image.Width);
    }

    [Fact]
    public async Task FormatConversionRequest_ReturnsCorrectContentType()
    {
        await WriteSourceAsync("format.png", TestImages.SolidColorPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(
            urls.Signed("/media/format.png", ("width", "20"), ("format", "webp")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CropRequest_SucceedsAtTheHttpLevel()
    {
        await WriteSourceAsync("crop.png", TestImages.SolidColorPngBytes(100, 100));

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(
            urls.Signed("/media/crop.png", ("width", "50"), ("cc", "0.1,0.1,0.1,0.1")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(50, image.Width);
    }

    [Fact]
    public async Task CorrectlySignedRequest_IsAccepted()
    {
        await WriteSourceAsync("hmac-accept.png", TestImages.FourCornerPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(urls.Signed("/media/hmac-accept.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TamperedRequest_IsRejected()
    {
        await WriteSourceAsync("hmac-tampered.png", TestImages.FourCornerPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(SignedRequestUrlBuilder.Tampered("/media/hmac-tampered.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsignedRequest_IsRejected()
    {
        await WriteSourceAsync("hmac-unsigned.png", TestImages.FourCornerPngBytes());

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using HttpResponseMessage response = await client.GetAsync(SignedRequestUrlBuilder.Unsigned("/media/hmac-unsigned.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task WriteSourceAsync(string relativePath, byte[] bytes)
    {
        string fullPath = Path.Combine(_mediaRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageProcessing:OriginalsRootPath"] = _mediaRoot,
                ["ImageProcessing:DerivativeCacheRootPath"] = _cacheRoot,
                ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(_hmacSecretKey),
            })));
}
