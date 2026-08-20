namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// Query-string command names. Matches the stock SixLabors.ImageSharp.Web command surface
/// (width, height, format, quality, bgcolor) plus Umbraco's own crop command (cc), so
/// existing image URLs work unchanged against any processor built on this Core.
/// </summary>
public static class ImageProcessingCommandNames
{
    public const string Width = "width";
    public const string Height = "height";
    public const string Format = "format";
    public const string Quality = "quality";
    public const string BackgroundColor = "bgcolor";
    public const string AutoOrient = "autoorient";
    public const string Crop = "cc";
    public const string HmacToken = "hmac";
}
