using SkiaSharp;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.SkiaSharp;

/// <summary>
/// Reference <see cref="IImageProcessor" /> implementation built directly against the raw
/// <c>SkiaSharp</c> NuGet package — no existing "SkiaSharp.Web" middleware exists, so decode, crop,
/// orientation-correction, resize, background flattening, and encode are all implemented here.
/// </summary>
public sealed class SkiaSharpImageProcessor : IImageProcessor
{
    /// <summary>
    /// SkiaSharp's native encoder only supports these three output formats — <c>gif</c> and
    /// <c>bmp</c> requests fail with <see cref="NotSupportedException" /> (see <see cref="Encode" />).
    /// </summary>
    private static readonly string[] EncodableFormats = ["jpg", "jpeg", "png", "webp"];

    public string Name => "SkiaSharp";

    public IReadOnlyCollection<string> SupportedOutputFormats { get; } = EncodableFormats;

    public Task ProcessAsync(Stream source, Stream destination, ResolvedImageCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using SKBitmap decoded = SKBitmap.Decode(source) ??
            throw new InvalidOperationException("SkiaSharp could not decode the source image.");

        SKBitmap current = decoded;
        var owned = new List<SKBitmap>();
        try
        {
            if (command.Crop is { } crop)
            {
                current = Crop(current, crop);
                owned.Add(current);
            }

            if (command.ExifOrientation is not (ExifOrientation.TopLeft or ExifOrientation.Unknown))
            {
                current = ApplyOrientation(current, command.ExifOrientation);
                owned.Add(current);
            }

            (int targetWidth, int targetHeight) = ComputeTargetSize(current.Width, current.Height, command.Width, command.Height);
            if (targetWidth != current.Width || targetHeight != current.Height)
            {
                current = Resize(current, targetWidth, targetHeight);
                owned.Add(current);
            }

            if (command.BackgroundColor is { } backgroundColor)
            {
                current = ApplyBackground(current, backgroundColor);
                owned.Add(current);
            }

            Encode(current, command.Format, command.Quality, destination);
        }
        finally
        {
            foreach (SKBitmap bitmap in owned)
            {
                bitmap.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    private static SKBitmap Crop(SKBitmap source, CropRectangle crop)
    {
        int x = Math.Clamp(crop.X, 0, Math.Max(source.Width - 1, 0));
        int y = Math.Clamp(crop.Y, 0, Math.Max(source.Height - 1, 0));
        int width = Math.Clamp(crop.Width, 1, source.Width - x);
        int height = Math.Clamp(crop.Height, 1, source.Height - y);

        var rect = SKRectI.Create(x, y, width, height);
        var cropped = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        if (!source.ExtractSubset(cropped, rect))
        {
            cropped.Dispose();
            throw new InvalidOperationException("SkiaSharp could not extract the crop rectangle.");
        }

        return cropped;
    }

    /// <summary>
    /// Applies the resolved EXIF orientation via an exact pixel-mapping matrix derived directly from
    /// <see cref="ExifOrientationTransform.Transform" /> (each case below is that function's
    /// normalized-space mapping restated in pixel space) — not a hand-composed sequence of canvas
    /// rotate/flip calls, which would be easy to get subtly wrong for the four transpose orientations.
    /// </summary>
    private static SKBitmap ApplyOrientation(SKBitmap source, ushort orientation)
    {
        bool swap = ExifOrientationTransform.IsRotated(orientation);
        int width = swap ? source.Height : source.Width;
        int height = swap ? source.Width : source.Height;

        SKMatrix matrix = orientation switch
        {
            ExifOrientation.TopRight => new SKMatrix(-1, 0, source.Width, 0, 1, 0, 0, 0, 1),
            ExifOrientation.BottomRight => new SKMatrix(-1, 0, source.Width, 0, -1, source.Height, 0, 0, 1),
            ExifOrientation.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, source.Height, 0, 0, 1),
            ExifOrientation.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            ExifOrientation.RightTop => new SKMatrix(0, 1, 0, -1, 0, source.Width, 0, 0, 1),
            ExifOrientation.RightBottom => new SKMatrix(0, -1, source.Height, -1, 0, source.Width, 0, 0, 1),
            ExifOrientation.LeftBottom => new SKMatrix(0, -1, source.Height, 1, 0, 0, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var oriented = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(oriented);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        return oriented;
    }

    private static (int Width, int Height) ComputeTargetSize(int currentWidth, int currentHeight, int? requestedWidth, int? requestedHeight)
    {
        if (requestedWidth is int w && requestedHeight is int h)
        {
            return (w, h);
        }

        if (requestedWidth is int widthOnly)
        {
            int height = Math.Max(1, (int)Math.Round(currentHeight * (widthOnly / (double)currentWidth)));
            return (widthOnly, height);
        }

        if (requestedHeight is int heightOnly)
        {
            int width = Math.Max(1, (int)Math.Round(currentWidth * (heightOnly / (double)currentHeight)));
            return (width, heightOnly);
        }

        return (currentWidth, currentHeight);
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        var info = new SKImageInfo(width, height, source.ColorType, source.AlphaType, source.ColorSpace);
        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) ??
            throw new InvalidOperationException("SkiaSharp could not resize the image.");
    }

    private static SKBitmap ApplyBackground(SKBitmap source, ImageColor backgroundColor)
    {
        var flattened = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(flattened);
        canvas.Clear(new SKColor(backgroundColor.R, backgroundColor.G, backgroundColor.B, backgroundColor.A));
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        return flattened;
    }

    private static void Encode(SKBitmap bitmap, string format, int quality, Stream destination)
    {
        SKEncodedImageFormat encodedFormat = format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "png" => SKEncodedImageFormat.Png,
            "webp" => SKEncodedImageFormat.Webp,
            _ => throw new NotSupportedException(
                $"The SkiaSharp processor cannot encode to '{format}'. Supported formats: {string.Join(", ", EncodableFormats)}."),
        };

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(encodedFormat, quality);
        data.SaveTo(destination);
    }
}
