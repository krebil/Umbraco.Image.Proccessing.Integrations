using Umbraco.Image.Processing.Core.Commands;

namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// The fully-resolved instructions handed to an <see cref="IImageProcessor" />: normalized commands
/// plus source-image-dependent math (crop rectangle, EXIF orientation) already computed by Core, so
/// a processor only has to decode, transform, and encode — no focal-point or orientation logic of
/// its own required.
/// </summary>
public sealed record ResolvedImageCommand
{
    public int? Width { get; init; }

    public int? Height { get; init; }

    public required string Format { get; init; }

    public required int Quality { get; init; }

    public ImageColor? BackgroundColor { get; init; }

    /// <summary>
    /// The pixel-space crop rectangle, or <see langword="null" /> for no crop.
    /// </summary>
    public CropRectangle? Crop { get; init; }

    /// <summary>
    /// The EXIF orientation to correct for, or <see cref="ExifOrientation.TopLeft" /> (a no-op) when
    /// auto-orientation is disabled or the source carries no orientation tag.
    /// </summary>
    public required ushort ExifOrientation { get; init; }
}
