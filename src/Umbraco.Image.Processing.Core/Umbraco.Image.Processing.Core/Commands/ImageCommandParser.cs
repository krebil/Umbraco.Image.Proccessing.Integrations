using System.Globalization;
using Microsoft.AspNetCore.Http;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// Parses and validates a request's query string into a <see cref="ParsedImageCommand" />. Width/height
/// clamping mirrors the standalone-service plan's <c>OnParseCommandsAsync</c>: out-of-range values are
/// dropped rather than rejecting the request.
/// </summary>
public static class ImageCommandParser
{
    public static ParsedImageCommand Parse(IQueryCollection query, ImageProcessingOptions options)
    {
        int? width = ParseClampedDimension(query, ImageProcessingCommandNames.Width, options.MaxWidth);
        int? height = ParseClampedDimension(query, ImageProcessingCommandNames.Height, options.MaxHeight);

        string? format = null;
        if (query.TryGetValue(ImageProcessingCommandNames.Format, out var formatValue))
        {
            string normalized = formatValue.ToString().Trim().ToLowerInvariant();
            if (options.SupportedOutputFormats.Contains(normalized))
            {
                format = normalized;
            }
        }

        int? quality = null;
        if (query.TryGetValue(ImageProcessingCommandNames.Quality, out var qualityValue) &&
            int.TryParse(qualityValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int q))
        {
            quality = Math.Clamp(q, 1, 100);
        }

        ImageColor? backgroundColor = null;
        if (query.TryGetValue(ImageProcessingCommandNames.BackgroundColor, out var bgValue) &&
            ImageColor.TryParseHex(bgValue.ToString(), out ImageColor color))
        {
            backgroundColor = color;
        }

        bool autoOrient = true;
        if (query.TryGetValue(ImageProcessingCommandNames.AutoOrient, out var autoOrientValue) &&
            bool.TryParse(autoOrientValue.ToString(), out bool parsedAutoOrient))
        {
            autoOrient = parsedAutoOrient;
        }

        ImageCropCoordinates? crop = null;
        if (query.TryGetValue(ImageProcessingCommandNames.Crop, out var cropValue) &&
            ImageCropCoordinates.TryParse(cropValue.ToString(), out ImageCropCoordinates coordinates))
        {
            crop = coordinates;
        }

        return new ParsedImageCommand
        {
            Width = width,
            Height = height,
            Format = format,
            Quality = quality,
            BackgroundColor = backgroundColor,
            AutoOrient = autoOrient,
            Crop = crop,
        };
    }

    private static int? ParseClampedDimension(IQueryCollection query, string key, int max)
    {
        if (!query.TryGetValue(key, out var raw))
        {
            return null;
        }

        if (!int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return null;
        }

        return value <= 0 || value >= max ? null : value;
    }
}
