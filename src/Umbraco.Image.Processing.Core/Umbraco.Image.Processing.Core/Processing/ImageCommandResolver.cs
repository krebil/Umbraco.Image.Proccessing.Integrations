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

        return new ResolvedImageCommand
        {
            Width = parsed.Width,
            Height = parsed.Height,
            Format = parsed.Format ?? sourceHeader.Format,
            Quality = parsed.Quality ?? options.DefaultQuality,
            BackgroundColor = parsed.BackgroundColor,
            Crop = crop,
            ExifOrientation = orientation,
        };
    }
}
