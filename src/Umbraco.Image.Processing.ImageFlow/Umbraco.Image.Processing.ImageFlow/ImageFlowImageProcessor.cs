using Imageflow.Fluent;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.ImageFlow;

/// <summary>
/// <see cref="IImageProcessor" /> implementation built directly against <c>Imageflow.NET</c>'s
/// in-process fluent job API (<see cref="ImageJob" />/<see cref="BuildNode" />,
/// <c>Finish().InProcessAsync()</c>) — <c>Imageflow.Server</c> is never involved, per the research
/// ticket's recommendation (its own middleware, query vocabulary, cache, and license-watermarking
/// would all conflict with Core's already-decided ownership of those concerns).
/// </summary>
/// <remarks>
/// Actually executing a job (<c>InProcessAsync()</c>, which every call to <see cref="ProcessAsync" />
/// does) requires AGPLv3 compliance or a commercial imazen license — independent of this integration
/// choice. See the quickstart docs for details.
/// </remarks>
public sealed class ImageFlowImageProcessor : IImageProcessor
{
    /// <summary>
    /// Formats Imageflow.NET has a typed <see cref="IEncoderPreset" /> for (see <see cref="CreateEncoder" />).
    /// Unlike SkiaSharp, this includes <c>gif</c>; unlike stock ImageSharp.Web, neither processor can
    /// encode <c>bmp</c> — no Imageflow.NET encoder preset exists for it.
    /// </summary>
    private static readonly string[] EncodableFormats = ["jpg", "jpeg", "png", "webp", "gif"];

    public string Name => "ImageFlow";

    public IReadOnlyCollection<string> SupportedOutputFormats { get; } = EncodableFormats;

    public async Task ProcessAsync(Stream source, Stream destination, ResolvedImageCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] sourceBytes = await ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);

        if (!ImageHeaderReader.TryRead(new MemoryStream(sourceBytes), out ImageHeaderInfo header))
        {
            throw new InvalidOperationException("Imageflow could not read the source image header.");
        }

        using var job = new ImageJob();
        BuildNode node = job.Decode(sourceBytes);

        if (command.Crop is { } crop)
        {
            (int x, int y, int width, int height) = ClampCrop(crop, header.Width, header.Height);
            node = node.Crop(x, y, x + width, y + height);
        }

        if (command.ExifOrientation is not (ExifOrientation.TopLeft or ExifOrientation.Unknown))
        {
            node = ApplyOrientation(node, command.ExifOrientation);
        }

        if (command.ModeCrop is { } modeCrop)
        {
            // Already computed in post-crop/post-orientation pixel space (see ImageCommandResolver),
            // so it applies directly with no extra clamping context needed beyond its own bounds.
            node = node.Crop(modeCrop.X, modeCrop.Y, modeCrop.X + modeCrop.Width, modeCrop.Y + modeCrop.Height);
        }

        int resizeWidth = command.Placement?.Width ?? command.Width;
        int resizeHeight = command.Placement?.Height ?? command.Height;
        node = node.Constrain(new Constraint(ConstraintMode.Distort, (uint)resizeWidth, (uint)resizeHeight));

        if (command.Placement is { } placement)
        {
            // The Pad/BoxPad resize modes' letterboxing step: place the just-resized image at its
            // offset within the full Width x Height canvas, filling the remainder with the pad color.
            // Verified directly against ImageFlowImageProcessorTests' diagnostic cases — ExpandCanvas
            // itself composites the given color correctly; running the separate BackgroundColor
            // flatten step (below) afterward on top of it is what corrupted the result, since that
            // step's own Within_Pad-based reprocessing doesn't just no-op on an already-canvas-sized
            // image. So this and that step are mutually exclusive, not sequential.
            int right = command.Width - placement.X - placement.Width;
            int bottom = command.Height - placement.Y - placement.Height;
            ImageColor padColor = command.BackgroundColor ?? new ImageColor(0, 0, 0, 0);
            node = node.ExpandCanvas(placement.X, placement.Y, right, bottom, ToAnyColor(padColor));
        }
        else if (command.BackgroundColor is { } backgroundColor)
        {
            // Imageflow has no dedicated "flatten transparency onto a color" node: Region/Crop copy pixels
            // (including alpha) unchanged, and Constrain's canvas_color is documented only for letterboxing
            // added by padding. Empirically (see ImageFlowImageProcessorTests), a pad-mode Constrain still
            // composites the source over its canvas color even when the target size exactly matches the
            // current size (no actual padding), which is what flattens transparent pixels here.
            var constraint = new Constraint(ConstraintMode.Within_Pad, (uint)command.Width, (uint)command.Height).SetCanvasColor(ToAnyColor(backgroundColor));
            node = node.Constrain(constraint);
        }

        BuildEndpoint endpoint = node.EncodeToStream(destination, disposeStream: false, CreateEncoder(command.Format, command.Quality));
        await endpoint.Finish().WithCancellationToken(cancellationToken).InProcessAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the resolved EXIF orientation via Imageflow's discrete rotate/flip/transpose nodes — no
    /// matrix primitive exists on Imageflow's graph, so each of the eight standard orientations is
    /// composed from these instead. Verified empirically against a known four-corner test image
    /// (<see cref="ImageFlowImageProcessorTests" />): Imageflow's <c>Rotate90()</c>/<c>Rotate270()</c>
    /// name their rotation direction opposite to <see cref="ExifOrientationTransform" />'s
    /// <c>RightTop</c>/<c>LeftBottom</c> cases, so the two are swapped relative to the naive mapping.
    /// </summary>
    private static BuildNode ApplyOrientation(BuildNode node, ushort orientation) => orientation switch
    {
        ExifOrientation.TopRight => node.FlipHorizontal(),
        ExifOrientation.BottomRight => node.Rotate180(),
        ExifOrientation.BottomLeft => node.FlipVertical(),
        ExifOrientation.LeftTop => node.Transpose(),
        ExifOrientation.RightTop => node.Rotate270(),
        ExifOrientation.RightBottom => node.Transpose().Rotate180(),
        ExifOrientation.LeftBottom => node.Rotate90(),
        _ => node,
    };

    private static (int X, int Y, int Width, int Height) ClampCrop(CropRectangle crop, int sourceWidth, int sourceHeight)
    {
        int x = Math.Clamp(crop.X, 0, Math.Max(sourceWidth - 1, 0));
        int y = Math.Clamp(crop.Y, 0, Math.Max(sourceHeight - 1, 0));
        int width = Math.Clamp(crop.Width, 1, sourceWidth - x);
        int height = Math.Clamp(crop.Height, 1, sourceHeight - y);
        return (x, y, width, height);
    }

    private static AnyColor ToAnyColor(ImageColor color) => AnyColor.Srgb(new SrgbColor(color.R, color.G, color.B, color.A));

    private static IEncoderPreset CreateEncoder(string format, int quality) => format.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => new MozJpegEncoder(quality),
        "png" => new LodePngEncoder(),
        "webp" => new WebPLossyEncoder(quality),
        "gif" => new GifEncoder(),
        _ => throw new NotSupportedException(
            $"The ImageFlow processor cannot encode to '{format}'. Supported formats: {string.Join(", ", EncodableFormats)}."),
    };

    private static async Task<byte[]> ReadAllBytesAsync(Stream source, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
