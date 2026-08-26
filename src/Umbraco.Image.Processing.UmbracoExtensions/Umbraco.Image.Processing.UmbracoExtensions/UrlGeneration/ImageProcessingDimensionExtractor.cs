using System.Drawing;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Media;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;

/// <summary>
/// The single <see cref="IImageDimensionExtractor" /> shared by every processor (backoffice Image
/// Cropper preview). Reads the source header directly — no processor-specific decode needed.
/// </summary>
public sealed class ImageProcessingDimensionExtractor(IOptions<ImageProcessingOptions> options) : IImageDimensionExtractor
{
    private readonly ImageProcessingOptions _options = options.Value;

    public IEnumerable<string> SupportedImageFileTypes => _options.SupportedRequestExtensions.Select(e => e.TrimStart('.'));

    public Size? GetDimensions(Stream stream)
    {
        if (!ImageHeaderReader.TryRead(stream, out ImageHeaderInfo header))
        {
            return null;
        }

        return ExifOrientationTransform.IsRotated(header.ExifOrientation)
            ? new Size(header.Height, header.Width)
            : new Size(header.Width, header.Height);
    }
}
