namespace Umbraco.Image.Processing.Core.Media;

/// <summary>
/// Dimensions, format, and (JPEG-only) EXIF orientation read straight from an image's container
/// header — no decode required.
/// </summary>
public readonly record struct ImageHeaderInfo(int Width, int Height, string Format, ushort ExifOrientation);
