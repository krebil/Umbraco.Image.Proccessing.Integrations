using System.Numerics;

namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// Ported directly from <c>SixLabors.ImageSharp.Web.ExifOrientationUtilities</c>
/// (github.com/SixLabors/ImageSharp.Web, src/ImageSharp.Web/ExifOrientationUtilities.cs) — the same
/// math Umbraco's own <c>CropWebProcessor</c> uses to make its crop rectangle orientation-aware.
/// </summary>
public static class ExifOrientationTransform
{
    public static Vector2 Transform(Vector2 position, Vector2 min, Vector2 max, ushort orientation)
    {
        Vector2 bounds = max - min;
        return orientation switch
        {
            // 0 degrees, mirrored: image has been flipped back-to-front.
            ExifOrientation.TopRight => new Vector2(Flip(position.X, bounds.X), position.Y),

            // 180 degrees: image is upside down.
            ExifOrientation.BottomRight => new Vector2(Flip(position.X, bounds.X), Flip(position.Y, bounds.Y)),

            // 180 degrees, mirrored: image has been flipped back-to-front and is upside down.
            ExifOrientation.BottomLeft => new Vector2(position.X, Flip(position.Y, bounds.Y)),

            // 90 degrees: image has been flipped back-to-front and is on its side.
            ExifOrientation.LeftTop => new Vector2(position.Y, position.X),

            // 90 degrees, mirrored: image is on its side.
            ExifOrientation.RightTop => new Vector2(position.Y, Flip(position.X, bounds.X)),

            // 270 degrees: image has been flipped back-to-front and is on its far side.
            ExifOrientation.RightBottom => new Vector2(Flip(position.Y, bounds.Y), Flip(position.X, bounds.X)),

            // 270 degrees, mirrored: image is on its far side.
            ExifOrientation.LeftBottom => new Vector2(Flip(position.Y, bounds.Y), position.X),

            // 0 degrees: the correct orientation, no adjustment required.
            _ => position,
        };
    }

    public static bool IsRotated(ushort orientation) => orientation switch
    {
        ExifOrientation.LeftTop or ExifOrientation.RightTop or ExifOrientation.RightBottom or ExifOrientation.LeftBottom => true,
        _ => false,
    };

    private static float Flip(float offset, float max) => max - offset;
}
