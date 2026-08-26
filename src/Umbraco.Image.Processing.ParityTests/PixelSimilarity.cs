using SkiaSharp;

namespace Umbraco.Image.Processing.ParityTests;

/// <summary>
/// Compares two same-sized decoded bitmaps for "close enough" pixel equivalence between two
/// independent encoders/decoders, rather than requiring byte-identical or pixel-identical output.
/// SkiaSharp and ImageFlow legitimately use different resample filters and encoders, so exact
/// per-pixel equality is the wrong bar (see ADR-0003) -- this measures how far apart they are and
/// lets the caller assert against a documented threshold.
/// </summary>
internal static class PixelSimilarity
{
    /// <summary>
    /// Per-channel (R, G, B, A) absolute-difference summary across every pixel in the pair.
    /// </summary>
    internal readonly record struct Result(double MeanChannelDelta, int MaxChannelDelta, double OutlierPixelRatio)
    {
        public override string ToString() =>
            $"meanChannelDelta={MeanChannelDelta:F2}, maxChannelDelta={MaxChannelDelta}, outlierPixelRatio={OutlierPixelRatio:P1}";
    }

    /// <summary>
    /// Compares every pixel of <paramref name="expected" /> and <paramref name="actual" />, which must
    /// already be the same dimensions (callers assert that separately, since a dimension mismatch is a
    /// different failure mode than a pixel-value mismatch).
    /// </summary>
    /// <param name="outlierChannelDeltaThreshold">
    /// A single channel's absolute delta (0-255) above which a pixel counts as an "outlier" -- localized
    /// differences (e.g. anti-aliased edges between two resample filters, or JPEG block artifacts) are
    /// expected and shouldn't fail the whole comparison the way a single very-different pixel would if
    /// only a max-delta bound were used.
    /// </param>
    internal static Result Compare(SKBitmap expected, SKBitmap actual, int outlierChannelDeltaThreshold)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            throw new ArgumentException(
                $"Cannot compare pixels of differently-sized bitmaps ({expected.Width}x{expected.Height} vs {actual.Width}x{actual.Height}).");
        }

        long channelDeltaSum = 0;
        long channelCount = 0;
        int maxChannelDelta = 0;
        long outlierPixels = 0;

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                SKColor e = expected.GetPixel(x, y);
                SKColor a = actual.GetPixel(x, y);

                int dr = Math.Abs(e.Red - a.Red);
                int dg = Math.Abs(e.Green - a.Green);
                int db = Math.Abs(e.Blue - a.Blue);
                int da = Math.Abs(e.Alpha - a.Alpha);

                channelDeltaSum += dr + dg + db + da;
                channelCount += 4;

                int pixelMaxDelta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                if (pixelMaxDelta > maxChannelDelta)
                {
                    maxChannelDelta = pixelMaxDelta;
                }

                if (pixelMaxDelta > outlierChannelDeltaThreshold)
                {
                    outlierPixels++;
                }
            }
        }

        double meanChannelDelta = channelDeltaSum / (double)channelCount;
        double outlierPixelRatio = outlierPixels / (double)(expected.Width * expected.Height);
        return new Result(meanChannelDelta, maxChannelDelta, outlierPixelRatio);
    }
}
