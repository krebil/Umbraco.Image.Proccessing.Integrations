using SkiaSharp;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.ImageFlow.Tests;

/// <summary>
/// Uses SkiaSharp purely as an independent test fixture builder/decoder — proven correct by
/// <c>Umbraco.Image.Processing.SkiaSharp.Tests</c> — so these tests verify Imageflow's own pixel
/// output rather than trusting Imageflow to check itself.
/// </summary>
public class ImageFlowImageProcessorTests
{
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 255, 0);
    private static readonly SKColor Blue = new(0, 0, 255);
    private static readonly SKColor Yellow = new(255, 255, 0);

    /// <summary>
    /// A 2x2 PNG with a distinct color in every corner: top-left=red, top-right=green,
    /// bottom-left=blue, bottom-right=yellow. Small and asymmetric enough to pin down exactly where
    /// each pixel lands after crop/orientation/resize.
    /// </summary>
    private static MemoryStream FourCornerPng()
    {
        using var bitmap = new SKBitmap(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.SetPixel(0, 0, Red);
        bitmap.SetPixel(1, 0, Green);
        bitmap.SetPixel(0, 1, Blue);
        bitmap.SetPixel(1, 1, Yellow);

        var stream = new MemoryStream();
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    private static ResolvedImageCommand Command(
        int? width = null,
        int? height = null,
        string format = "png",
        int quality = 100,
        ImageColor? backgroundColor = null,
        CropRectangle? crop = null,
        ushort exifOrientation = ExifOrientation.TopLeft) =>
        new()
        {
            Width = width,
            Height = height,
            Format = format,
            Quality = quality,
            BackgroundColor = backgroundColor,
            Crop = crop,
            ExifOrientation = exifOrientation,
        };

    private static SKBitmap Decode(MemoryStream stream)
    {
        stream.Position = 0;
        return SKBitmap.Decode(stream) ?? throw new InvalidOperationException("Test setup produced an undecodable image.");
    }

    [Fact]
    public async Task ProcessAsync_NoCommands_PreservesDimensionsAndPixels()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command());

        using SKBitmap result = Decode(destination);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(Red, result.GetPixel(0, 0));
        Assert.Equal(Green, result.GetPixel(1, 0));
        Assert.Equal(Blue, result.GetPixel(0, 1));
        Assert.Equal(Yellow, result.GetPixel(1, 1));
    }

    [Fact]
    public async Task ProcessAsync_WidthAndHeight_ResizesToExactTarget()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(width: 10, height: 6));

        using SKBitmap result = Decode(destination);
        Assert.Equal(10, result.Width);
        Assert.Equal(6, result.Height);
    }

    [Fact]
    public async Task ProcessAsync_WidthOnly_PreservesAspectRatio()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(width: 8));

        using SKBitmap result = Decode(destination);
        Assert.Equal(8, result.Width);
        Assert.Equal(8, result.Height); // source is square (2x2), so aspect-preserving height matches width
    }

    [Fact]
    public async Task ProcessAsync_Crop_ExtractsCorrectRegion()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        // Top-right 1x1 quadrant only.
        await processor.ProcessAsync(source, destination, Command(crop: new CropRectangle(1, 0, 1, 1)));

        using SKBitmap result = Decode(destination);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(Green, result.GetPixel(0, 0));
    }

    [Fact]
    public async Task ProcessAsync_Orientation_RightTop_Rotates90Clockwise()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(exifOrientation: ExifOrientation.RightTop));

        using SKBitmap result = Decode(destination);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        // 90deg clockwise: top-left(red) -> bottom-left, bottom-right(yellow) -> top-right.
        Assert.Equal(Red, result.GetPixel(0, 1));
        Assert.Equal(Yellow, result.GetPixel(1, 0));
    }

    [Fact]
    public async Task ProcessAsync_Orientation_LeftTop_Transposes()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(exifOrientation: ExifOrientation.LeftTop));

        using SKBitmap result = Decode(destination);
        // Transpose across the top-left/bottom-right diagonal: corners on the diagonal are fixed,
        // the other two (top-right/bottom-left) swap.
        Assert.Equal(Red, result.GetPixel(0, 0));
        Assert.Equal(Yellow, result.GetPixel(1, 1));
        Assert.Equal(Blue, result.GetPixel(1, 0));
        Assert.Equal(Green, result.GetPixel(0, 1));
    }

    [Fact]
    public async Task ProcessAsync_Orientation_BottomRight_Rotates180()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(exifOrientation: ExifOrientation.BottomRight));

        using SKBitmap result = Decode(destination);
        Assert.Equal(Yellow, result.GetPixel(0, 0));
        Assert.Equal(Red, result.GetPixel(1, 1));
    }

    [Fact]
    public async Task ProcessAsync_Orientation_RightBottom_TransposesAndRotates180()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(exifOrientation: ExifOrientation.RightBottom));

        using SKBitmap result = Decode(destination);
        // Transverse (flip across the anti-diagonal): corners on that diagonal (top-right/bottom-left) are
        // fixed, the other two (top-left/bottom-right) swap.
        Assert.Equal(Green, result.GetPixel(1, 0));
        Assert.Equal(Blue, result.GetPixel(0, 1));
        Assert.Equal(Yellow, result.GetPixel(0, 0));
        Assert.Equal(Red, result.GetPixel(1, 1));
    }

    [Fact]
    public async Task ProcessAsync_Orientation_LeftBottom_Rotates270Clockwise()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(exifOrientation: ExifOrientation.LeftBottom));

        using SKBitmap result = Decode(destination);
        // 270deg clockwise (=90 CCW): top-left(red) -> top-right, bottom-right(yellow) -> bottom-left.
        Assert.Equal(Red, result.GetPixel(1, 0));
        Assert.Equal(Yellow, result.GetPixel(0, 1));
    }

    [Fact]
    public async Task ProcessAsync_BackgroundColor_FlattensTransparentPixels()
    {
        using var bitmap = new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.SetPixel(0, 0, new SKColor(0, 0, 0, 0));
        var source = new MemoryStream();
        using (SKImage image = SKImage.FromBitmap(bitmap))
        using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(source);
        }

        source.Position = 0;
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(backgroundColor: new ImageColor(255, 0, 0, 255)));

        using SKBitmap result = Decode(destination);
        Assert.Equal(new SKColor(255, 0, 0, 255), result.GetPixel(0, 0));
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedFormat_Throws()
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => processor.ProcessAsync(source, destination, Command(format: "bmp")));
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    [InlineData("gif")]
    public async Task ProcessAsync_SupportedFormats_EncodeSuccessfully(string format)
    {
        using MemoryStream source = FourCornerPng();
        var destination = new MemoryStream();
        var processor = new ImageFlowImageProcessor();

        await processor.ProcessAsync(source, destination, Command(format: format, quality: 80));

        Assert.True(destination.Length > 0);
    }

    [Fact]
    public void Name_IsImageFlow()
    {
        Assert.Equal("ImageFlow", new ImageFlowImageProcessor().Name);
    }

    [Fact]
    public void SupportedOutputFormats_IncludesGifExcludesBmp()
    {
        IReadOnlyCollection<string> formats = new ImageFlowImageProcessor().SupportedOutputFormats;
        Assert.Contains("png", formats);
        Assert.Contains("gif", formats);
        Assert.DoesNotContain("bmp", formats);
    }
}
