namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// The seam a processor project (SkiaSharp, ImageFlow, ...) implements: decode <paramref name="source" />,
/// apply <paramref name="command" />, encode to <paramref name="destination" />. Everything
/// processor-agnostic — command parsing, crop-rectangle math, EXIF-orientation resolution, HMAC
/// signing, response writing — already happened in Core before this is called.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// A short, human-readable name for diagnostics (e.g. "SkiaSharp", "ImageFlow").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Output formats this processor can encode to.
    /// </summary>
    IReadOnlyCollection<string> SupportedOutputFormats { get; }

    Task ProcessAsync(Stream source, Stream destination, ResolvedImageCommand command, CancellationToken cancellationToken = default);
}
