using System.Globalization;

namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// Umbraco's <c>cc</c> crop/focal-point command: four normalized (0..1) distances from the
/// left, top, right, and bottom edges of the source image.
/// </summary>
public readonly record struct ImageCropCoordinates(float Left, float Top, float Right, float Bottom)
{
    public static bool TryParse(string? value, out ImageCropCoordinates coordinates)
    {
        coordinates = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        Span<float> values = stackalloc float[4];
        for (var i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        // All-zero means "no crop" — mirrors Umbraco's own CropWebProcessor.
        if (values[0] == 0 && values[1] == 0 && values[2] == 0 && values[3] == 0)
        {
            return false;
        }

        coordinates = new ImageCropCoordinates(values[0], values[1], values[2], values[3]);
        return true;
    }
}
