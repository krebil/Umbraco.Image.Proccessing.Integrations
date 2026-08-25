using SkiaSharp;

namespace Umbraco.Image.Processing.IntegrationTests.Shared;

/// <summary>
/// Tiny, deterministic source images for HTTP-level pipeline tests (production-hardening ticket 07).
/// Pixel correctness isn't these tests' job — that's the parity suite's (ticket 06) — so these images
/// only need to be valid, decodable, and large enough for a crop command to have something to crop.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// A 2x2 PNG, one solid color per corner — enough to prove a request round-tripped through the
    /// real pipeline (decodable, byte-identical on pass-through) without needing a bigger fixture.
    /// </summary>
    public static byte[] FourCornerPngBytes()
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
    /// A solid-color PNG at the given dimensions (default 100x100) — large enough for a resize,
    /// format-conversion, or <c>cc</c> crop command to produce a meaningfully different output.
    /// </summary>
    public static byte[] SolidColorPngBytes(int width = 100, int height = 100)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(new SKColor(64, 128, 192));

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
