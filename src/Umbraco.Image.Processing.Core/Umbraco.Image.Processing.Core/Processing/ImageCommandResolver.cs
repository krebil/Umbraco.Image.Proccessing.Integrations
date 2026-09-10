using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// Resolves a <see cref="ParsedImageCommand" /> against the source image's header info into a
/// <see cref="ResolvedImageCommand" /> ready for an <see cref="IImageProcessor" />.
/// </summary>
public static class ImageCommandResolver
{
    public static ResolvedImageCommand Resolve(ParsedImageCommand parsed, ImageProcessingOptions options, ImageHeaderInfo sourceHeader)
    {
        ushort orientation = parsed.AutoOrient ? sourceHeader.ExifOrientation : ExifOrientation.TopLeft;

        CropRectangle? crop = parsed.Crop is { } coordinates
            ? ImageCropCalculator.Compute(coordinates, sourceHeader.Width, sourceHeader.Height, orientation)
            : null;

        // The resize-mode math operates on the image as it exists right before resizing — after any
        // explicit `cc` crop and after EXIF-orientation correction, since both processors apply crop
        // and orientation before resize.
        int workingWidth = crop?.Width ?? sourceHeader.Width;
        int workingHeight = crop?.Height ?? sourceHeader.Height;
        if (ExifOrientationTransform.IsRotated(orientation))
        {
            (workingWidth, workingHeight) = (workingHeight, workingWidth);
        }

        ResizeResolution resize = ResizeCalculator.Resolve(workingWidth, workingHeight, parsed.Width, parsed.Height, parsed.Mode);

        return new ResolvedImageCommand
        {
            Width = resize.Width,
            Height = resize.Height,
            Format = parsed.Format ?? sourceHeader.Format,
            Quality = parsed.Quality ?? options.DefaultQuality,
            BackgroundColor = parsed.BackgroundColor,
            Crop = crop,
            ModeCrop = resize.Crop,
            Placement = resize.Placement,
            ExifOrientation = orientation,
        };
    }
}
