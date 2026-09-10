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
    /// <summary>The final output width — always resolved, even when the request only gave one dimension or none at all.</summary>
    public required int Width { get; init; }

    /// <summary>The final output height — always resolved, even when the request only gave one dimension or none at all.</summary>
    public required int Height { get; init; }

    public required string Format { get; init; }

    public required int Quality { get; init; }

    public ImageColor? BackgroundColor { get; init; }

    /// <summary>
    /// The explicit <c>cc</c> crop rectangle, in the source image's stored (pre-orientation) pixel
    /// space, or <see langword="null" /> when no <c>cc</c> command was given.
    /// </summary>
    public CropRectangle? Crop { get; init; }

    /// <summary>
    /// The <see cref="Commands.ResizeMode.Crop" /> resize-mode's own crop rectangle — computed in the
    /// image's pixel space as it exists right before resizing (after <see cref="Crop" /> and after
    /// EXIF-orientation correction), or <see langword="null" /> for any other mode. Applied as an
    /// ordinary second crop step, not composed with <see cref="Crop" />, so a processor's existing
    /// crop primitive handles both without new code.
    /// </summary>
    public CropRectangle? ModeCrop { get; init; }

    /// <summary>
    /// For <see cref="Commands.ResizeMode.Pad" />/<see cref="Commands.ResizeMode.BoxPad" />: resize to
    /// this rectangle's own width/height, then place it at its (X, Y) within a <see cref="Width" /> x
    /// <see cref="Height" /> canvas, filling the remainder with <see cref="BackgroundColor" /> (or
    /// transparent when unset). <see langword="null" /> for any other mode, meaning resize straight to
    /// (<see cref="Width" />, <see cref="Height" />) with no separate canvas step.
    /// </summary>
    public CropRectangle? Placement { get; init; }

    /// <summary>
    /// The EXIF orientation to correct for, or <see cref="ExifOrientation.TopLeft" /> (a no-op) when
    /// auto-orientation is disabled or the source carries no orientation tag.
    /// </summary>
    public required ushort ExifOrientation { get; init; }
}
