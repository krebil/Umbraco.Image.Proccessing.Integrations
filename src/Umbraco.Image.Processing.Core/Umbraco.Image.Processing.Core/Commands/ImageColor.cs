using System.Globalization;

namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// A normalized RGBA color, parsed from the <c>bgcolor</c> command's hex value
/// (<c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, or <c>#rrggbbaa</c>, with or without the leading <c>#</c>).
/// </summary>
public readonly record struct ImageColor(byte R, byte G, byte B, byte A)
{
    public static bool TryParseHex(string? value, out ImageColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        byte r, g, b, a = 255;
        switch (span.Length)
        {
            case 3:
                if (!TryNibble(span[0], out int r1) || !TryNibble(span[1], out int g1) || !TryNibble(span[2], out int b1))
                {
                    return false;
                }

                r = (byte)((r1 * 16) + r1);
                g = (byte)((g1 * 16) + g1);
                b = (byte)((b1 * 16) + b1);
                break;

            case 4:
                if (!TryNibble(span[0], out int r2) || !TryNibble(span[1], out int g2) ||
                    !TryNibble(span[2], out int b2) || !TryNibble(span[3], out int a2))
                {
                    return false;
                }

                r = (byte)((r2 * 16) + r2);
                g = (byte)((g2 * 16) + g2);
                b = (byte)((b2 * 16) + b2);
                a = (byte)((a2 * 16) + a2);
                break;

            case 6:
                if (!byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) ||
                    !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) ||
                    !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                {
                    return false;
                }

                break;

            case 8:
                if (!byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) ||
                    !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) ||
                    !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b) ||
                    !byte.TryParse(span[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
                {
                    return false;
                }

                break;

            default:
                return false;
        }

        color = new ImageColor(r, g, b, a);
        return true;
    }

    public override string ToString() =>
        A == 255 ? $"{R:x2}{G:x2}{B:x2}" : $"{R:x2}{G:x2}{B:x2}{A:x2}";

    private static bool TryNibble(char c, out int value)
    {
        switch (c)
        {
            case >= '0' and <= '9':
                value = c - '0';
                return true;
            case >= 'a' and <= 'f':
                value = c - 'a' + 10;
                return true;
            case >= 'A' and <= 'F':
                value = c - 'A' + 10;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
