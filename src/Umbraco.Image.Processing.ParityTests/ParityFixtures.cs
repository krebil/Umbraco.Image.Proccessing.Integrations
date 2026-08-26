using SkiaSharp;

namespace Umbraco.Image.Processing.ParityTests;

/// <summary>
/// Source-image builders for the parity suite, built with SkiaSharp purely as an independent,
/// already-proven fixture builder/decoder (see <c>ImageFlowImageProcessorTests</c> in the ImageFlow
/// test project for the same rationale) -- not a preference between the two processors under test.
/// </summary>
/// <remarks>
/// A 40x60 quadrant image (not the 2x2 four-corner pixel grid the per-processor unit tests use) so
/// resize/crop pixel-similarity comparisons have a meaningful area to average over: two different
/// resample filters agreeing on the color of one pixel proves little, but agreeing on the mean color
/// of a large solid region -- net of a thin band of differing anti-aliasing at its edges -- is a real
/// parity signal. The non-square, non-power-of-two size (40 wide, 60 tall; quadrants 20x30) also
/// guards against width/height being silently swapped by either processor.
/// </remarks>
internal static class ParityFixtures
{
    internal const int Width = 40;
    internal const int Height = 60;
    internal const int QuadrantWidth = Width / 2;
    internal const int QuadrantHeight = Height / 2;

    internal static readonly SKColor Red = new(220, 20, 20, 255);
    internal static readonly SKColor Green = new(20, 200, 20, 255);
    internal static readonly SKColor Blue = new(20, 20, 220, 255);
    internal static readonly SKColor Yellow = new(220, 210, 20, 255);

    /// <summary>
    /// Opaque quadrant image: top-left=red, top-right=green, bottom-left=blue, bottom-right=yellow.
    /// Used for resize, crop, autoorient, and format-conversion cases.
    /// </summary>
    internal static MemoryStream QuadrantOpaquePng() => EncodeQuadrants(alpha: 255);

    /// <summary>
    /// Same quadrant layout, but fully transparent (alpha=0) everywhere. Used for the <c>bgcolor</c>
    /// case: a processor flattening transparent pixels onto a background color should produce a
    /// uniformly-colored result regardless of what RGB value sat underneath the zero alpha, so this
    /// avoids any ambiguity about partial-alpha blending math differing between processors.
    /// </summary>
    internal static MemoryStream QuadrantTransparentPng() => EncodeQuadrants(alpha: 0);

    private static MemoryStream EncodeQuadrants(byte alpha)
    {
        using var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(bitmap))
        {
            Fill(canvas, 0, 0, QuadrantWidth, QuadrantHeight, WithAlpha(Red, alpha));
            Fill(canvas, QuadrantWidth, 0, QuadrantWidth, QuadrantHeight, WithAlpha(Green, alpha));
            Fill(canvas, 0, QuadrantHeight, QuadrantWidth, QuadrantHeight, WithAlpha(Blue, alpha));
            Fill(canvas, QuadrantWidth, QuadrantHeight, QuadrantWidth, QuadrantHeight, WithAlpha(Yellow, alpha));
        }

        var stream = new MemoryStream();
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    private static void Fill(SKCanvas canvas, int x, int y, int width, int height, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = false };
        canvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }

    private static SKColor WithAlpha(SKColor color, byte alpha) => color.WithAlpha(alpha);

    /// <summary>
    /// Decodes a fully-processed output stream. Both processors under test are trusted to produce a
    /// decodable image (that's what the per-processor unit tests already cover); this is only used to
    /// read back pixels/dimensions for comparison, matching the decode helper both unit test classes use.
    /// </summary>
    internal static SKBitmap Decode(MemoryStream stream)
    {
        stream.Position = 0;
        return SKBitmap.Decode(stream) ?? throw new InvalidOperationException("Parity suite produced an undecodable image.");
    }
}
