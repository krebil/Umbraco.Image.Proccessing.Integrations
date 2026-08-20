namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// The eight standard EXIF/TIFF orientation values. Mirrors
/// <c>SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifOrientationMode</c> so a processor can apply the
/// resolved orientation with its own library's rotate/flip primitives.
/// </summary>
public static class ExifOrientation
{
    public const ushort Unknown = 0;
    public const ushort TopLeft = 1;
    public const ushort TopRight = 2;
    public const ushort BottomRight = 3;
    public const ushort BottomLeft = 4;
    public const ushort LeftTop = 5;
    public const ushort RightTop = 6;
    public const ushort RightBottom = 7;
    public const ushort LeftBottom = 8;
}
