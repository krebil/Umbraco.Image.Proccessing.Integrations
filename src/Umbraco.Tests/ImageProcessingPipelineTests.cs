using System.Net;
using SkiaSharp;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;
using File = System.IO.File;

namespace Umbraco.Tests;

/// <summary>
/// HTTP-level pipeline coverage for the in-process <c>Umbraco</c> sample (production-hardening ticket
/// 07): pass-through, resize, format conversion, <c>cc</c> crop, and HMAC accept/tampered/unsigned — all
/// at the status-code/header/content-type level, mirroring <c>Service.Tests</c>'
/// <c>ImageProcessingPipelineTests</c> for the standalone Service. Pixel correctness is the parity
/// suite's job (ticket 06). These tests exist to catch middleware wiring/registration regressions in
/// this sample's own <c>Program.cs</c> — in particular that <c>app.UseImageProcessing()</c> stays
/// mounted before Umbraco's own pipeline in InProcess mode — that unit tests at the processor/cache seam
/// can't see.
/// </summary>
public sealed class ImageProcessingPipelineTests(UmbracoWebAppFixture fixture) : IClassFixture<UmbracoWebAppFixture>
{
    private readonly UmbracoWebAppFixture _fixture = fixture;
    private readonly SignedRequestUrlBuilder _urls = new(fixture.HmacSecretKey);

    [Fact]
    public async Task PassThroughRequest_ReturnsOriginalImageUnmodified()
    {
        byte[] original = TestImages.FourCornerPngBytes();
        await WriteSourceAsync("pass-through.png", original);

        using HttpResponseMessage response = await _fixture.Client.GetAsync(_urls.Signed("/media/pass-through.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(original, bytes);
    }

    [Fact]
    public async Task ResizeRequest_ReturnsExpectedDimensions()
    {
        await WriteSourceAsync("resize.png", TestImages.SolidColorPngBytes());

        using HttpResponseMessage response = await _fixture.Client.GetAsync(_urls.Signed("/media/resize.png", ("width", "40")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(40, image.Width);
    }

    [Fact]
    public async Task FormatConversionRequest_ReturnsCorrectContentType()
    {
        await WriteSourceAsync("format.png", TestImages.SolidColorPngBytes());

        using HttpResponseMessage response = await _fixture.Client.GetAsync(
            _urls.Signed("/media/format.png", ("width", "20"), ("format", "webp")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CropRequest_SucceedsAtTheHttpLevel()
    {
        await WriteSourceAsync("crop.png", TestImages.SolidColorPngBytes(100, 100));

        using HttpResponseMessage response = await _fixture.Client.GetAsync(
            _urls.Signed("/media/crop.png", ("width", "50"), ("cc", "0.1,0.1,0.1,0.1")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using SKBitmap image = SKBitmap.Decode(bytes);
        Assert.Equal(50, image.Width);
    }

    [Fact]
    public async Task CorrectlySignedRequest_IsAccepted()
    {
        await WriteSourceAsync("hmac-accept.png", TestImages.FourCornerPngBytes());

        using HttpResponseMessage response = await _fixture.Client.GetAsync(_urls.Signed("/media/hmac-accept.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TamperedRequest_IsRejected()
    {
        await WriteSourceAsync("hmac-tampered.png", TestImages.FourCornerPngBytes());

        using HttpResponseMessage response = await _fixture.Client.GetAsync(SignedRequestUrlBuilder.Tampered("/media/hmac-tampered.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsignedRequest_IsRejected()
    {
        await WriteSourceAsync("hmac-unsigned.png", TestImages.FourCornerPngBytes());

        using HttpResponseMessage response = await _fixture.Client.GetAsync(SignedRequestUrlBuilder.Unsigned("/media/hmac-unsigned.png", ("width", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task WriteSourceAsync(string relativePath, byte[] bytes)
    {
        string fullPath = Path.Combine(_fixture.MediaRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);
    }
}
