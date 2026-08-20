using System.Numerics;
using Umbraco.Image.Processing.Core.Commands;

namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// Resolves Umbraco's normalized <c>cc</c> crop coordinates into a pixel-space rectangle. Ported
/// directly from <c>Umbraco.Cms.Imaging.ImageSharp.ImageProcessors.CropWebProcessor</c>
/// (src/Umbraco.Cms.Imaging.ImageSharp/ImageProcessors/CropWebProcessor.cs) so every processor
/// crops identically without needing its own focal-point math — neither SkiaSharp nor Imageflow
/// has an equivalent primitive.
/// </summary>
public static class ImageCropCalculator
{
    public static CropRectangle Compute(ImageCropCoordinates coordinates, int imageWidth, int imageHeight, ushort exifOrientation)
    {
        // The right/bottom values are distances from those edges — convert to absolute
        // coordinates and transform into the image's stored (pre-orientation-correction) space.
        float left = Math.Clamp(coordinates.Left, 0, 1);
        float top = Math.Clamp(coordinates.Top, 0, 1);
        float right = Math.Clamp(1 - coordinates.Right, 0, 1);
        float bottom = Math.Clamp(1 - coordinates.Bottom, 0, 1);

        Vector2 xy1 = ExifOrientationTransform.Transform(new Vector2(left, top), Vector2.Zero, Vector2.One, exifOrientation);
        Vector2 xy2 = ExifOrientationTransform.Transform(new Vector2(right, bottom), Vector2.Zero, Vector2.One, exifOrientation);

        float leftPx = MathF.Min(xy1.X, xy2.X) * imageWidth;
        float topPx = MathF.Min(xy1.Y, xy2.Y) * imageHeight;
        float rightPx = MathF.Max(xy1.X, xy2.X) * imageWidth;
        float bottomPx = MathF.Max(xy1.Y, xy2.Y) * imageHeight;

        // Mirrors System.Drawing.Rectangle.Round(RectangleF): X/Y/Width/Height are each rounded
        // independently, not derived from already-rounded edges.
        var x = (int)MathF.Round(leftPx);
        var y = (int)MathF.Round(topPx);
        var width = (int)MathF.Round(rightPx - leftPx);
        var height = (int)MathF.Round(bottomPx - topPx);

        return new CropRectangle(x, y, Math.Max(width, 1), Math.Max(height, 1));
    }
}
