using SkiaSharp;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;
using Xunit.Abstractions;
using ImageFlowProcessor = Umbraco.Image.Processing.ImageFlow.ImageFlowImageProcessor;
using SkiaSharpProcessor = Umbraco.Image.Processing.SkiaSharp.SkiaSharpImageProcessor;

namespace Umbraco.Image.Processing.ParityTests;

/// <summary>
/// Production-hardening ticket 06: proves the two <see cref="IImageProcessor" /> implementations
/// (<see cref="SkiaSharpProcessor" />, <see cref="ImageFlowProcessor" />) are drop-in replacements
/// for each other -- same <see cref="ResolvedImageCommand" /> in, equivalent dimensions/format/pixels
/// out -- per ADR-0003. "Equivalent" deliberately does not mean byte-identical: the two encoders and
/// resample filters legitimately differ, so this suite measures pixel closeness against a documented
/// threshold (<see cref="PixelSimilarity" />) rather than requiring an exact match.
/// </summary>
public class CrossProcessorParityTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // Thresholds are deliberately different for the two encoding paths exercised below:
    //  - "Structural" cases (resize/crop/autoorient/bgcolor) all encode to PNG, a lossless format, so
    //    the only source of pixel disagreement is the two processors' independent resample filters --
    //    expected to show up as a thin band of differing anti-aliasing along hard quadrant edges, not
    //    a broad difference. A tight mean bound plus a generous-but-bounded outlier ratio (for that
    //    edge band) catches real drift without being flaky over sub-pixel filter differences.
    //  - "Format conversion" cases re-encode the *same*, already-resolved pixels through each
    //    processor's own lossy/palette encoder (jpg, webp, gif) -- some extra disagreement close to
    //    quadrant boundaries (DCT block edges, palette quantization) is expected on top of that, so the
    //    bounds are looser.
    //
    // The outlier-ratio bounds specifically are more generous than they look: that thin edge-disagreement
    // band is close to fixed-width in pixels, so shrinking the resize target (e.g. ResizeHeightOnlyAspect,
    // which downsamples to 10x15) shrinks the denominator faster than the numerator and pushes the ratio
    // up even though the actual disagreement hasn't grown. Verified empirically against every case in this
    // suite; the tightest-passing structural case sits at ~19% outliers, so 25% leaves real headroom
    // without being a no-op bound.
    private const double StructuralMeanChannelDeltaThreshold = 10.0;
    private const int StructuralOutlierChannelDeltaThreshold = 45;
    private const double StructuralOutlierPixelRatioThreshold = 0.25;

    private const double FormatMeanChannelDeltaThreshold = 20.0;
    private const int FormatOutlierChannelDeltaThreshold = 60;
    private const double FormatOutlierPixelRatioThreshold = 0.35;

    private enum SourceKind
    {
        Opaque,
        Transparent,
    }

    private sealed record ParityCase(string Name, ResolvedImageCommand Command, SourceKind Source = SourceKind.Opaque);

    private static readonly IReadOnlyDictionary<string, ParityCase> CasesByName = BuildCases().ToDictionary(c => c.Name);

    /// <summary>
    /// The format-conversion theory data: the intersection of both processors' own
    /// <see cref="IImageProcessor.SupportedOutputFormats" />, computed at runtime rather than
    /// hardcoded -- per the ticket, format coverage must track whatever each processor's own property
    /// already declares, not a second, separately-maintained list.
    /// </summary>
    private static readonly IReadOnlyList<string> SupportedFormatIntersection = [.. new SkiaSharpProcessor()
        .SupportedOutputFormats
        .Intersect(new ImageFlowProcessor().SupportedOutputFormats, StringComparer.OrdinalIgnoreCase)
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

    public static TheoryData<string> CaseNames() => [.. CasesByName.Keys];

    public static TheoryData<string> FormatNames() => [.. SupportedFormatIntersection];

    private static IEnumerable<ParityCase> BuildCases()
    {
        yield return new ParityCase("NoOp", Command());

        yield return new ParityCase(
            "ResizeExactDistort",
            Command(width: ParityFixtures.QuadrantWidth, height: ParityFixtures.QuadrantHeight));

        yield return new ParityCase(
            "ResizeWidthOnlyAspect",
            Command(width: ParityFixtures.Width * 2));

        yield return new ParityCase(
            "ResizeHeightOnlyAspect",
            Command(height: ParityFixtures.Height / 4));

        yield return new ParityCase(
            "CropTopRightQuadrant",
            Command(crop: new CropRectangle(ParityFixtures.QuadrantWidth, 0, ParityFixtures.QuadrantWidth, ParityFixtures.QuadrantHeight)));

        yield return new ParityCase(
            "CropLeftHalfThenResizeNarrower",
            Command(
                crop: new CropRectangle(0, 0, ParityFixtures.QuadrantWidth, ParityFixtures.Height),
                width: ParityFixtures.QuadrantWidth / 2,
                height: ParityFixtures.Height));

        yield return new ParityCase(
            "AutoOrient_RightTop_Rotates90Clockwise",
            Command(exifOrientation: ExifOrientation.RightTop));

        yield return new ParityCase(
            "AutoOrient_BottomRight_Rotates180",
            Command(exifOrientation: ExifOrientation.BottomRight));

        yield return new ParityCase(
            "AutoOrient_LeftTop_Transposes",
            Command(exifOrientation: ExifOrientation.LeftTop));

        yield return new ParityCase(
            "BackgroundColor_FlattensTransparentSourceToSolidColor",
            Command(backgroundColor: new ImageColor(30, 30, 30, 255)),
            SourceKind.Transparent);
    }

    private static ResolvedImageCommand Command(
        int? width = null,
        int? height = null,
        string format = "png",
        int quality = 100,
        ImageColor? backgroundColor = null,
        CropRectangle? crop = null,
        ushort exifOrientation = ExifOrientation.TopLeft) =>
        new()
        {
            Width = width,
            Height = height,
            Format = format,
            Quality = quality,
            BackgroundColor = backgroundColor,
            Crop = crop,
            ExifOrientation = exifOrientation,
        };

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task SkiaSharpAndImageFlow_ProduceEquivalentOutput(string caseName)
    {
        ParityCase testCase = CasesByName[caseName];

        using MemoryStream skiaOutput = await RunAsync(new SkiaSharpProcessor(), testCase);
        using MemoryStream imageFlowOutput = await RunAsync(new ImageFlowProcessor(), testCase);

        using SKBitmap skiaResult = ParityFixtures.Decode(skiaOutput);
        using SKBitmap imageFlowResult = ParityFixtures.Decode(imageFlowOutput);

        Assert.True(
            skiaResult.Width == imageFlowResult.Width && skiaResult.Height == imageFlowResult.Height,
            $"Dimension mismatch for '{caseName}': SkiaSharp={skiaResult.Width}x{skiaResult.Height}, " +
            $"ImageFlow={imageFlowResult.Width}x{imageFlowResult.Height}.");

        PixelSimilarity.Result similarity = PixelSimilarity.Compare(skiaResult, imageFlowResult, StructuralOutlierChannelDeltaThreshold);
        _output.WriteLine($"'{caseName}': {similarity}");

        AssertSimilar(caseName, similarity, StructuralMeanChannelDeltaThreshold, StructuralOutlierPixelRatioThreshold);
    }

    [Theory]
    [MemberData(nameof(FormatNames))]
    public async Task SkiaSharpAndImageFlow_ProduceEquivalentOutput_ForEachCommonlySupportedFormat(string format)
    {
        ResolvedImageCommand command = Command(format: format, quality: 85);

        byte[] skiaBytes;
        byte[] imageFlowBytes;
        using (MemoryStream skiaOutput = await RunAsync(new SkiaSharpProcessor(), command, SourceKind.Opaque))
        using (MemoryStream imageFlowOutput = await RunAsync(new ImageFlowProcessor(), command, SourceKind.Opaque))
        {
            skiaBytes = skiaOutput.ToArray();
            imageFlowBytes = imageFlowOutput.ToArray();
        }

        // SKCodec.Create takes ownership of (and disposes) the stream it's given, so each check below
        // gets its own fresh MemoryStream over the same bytes rather than sharing one with the decode
        // step further down.
        AssertEncodedFormat(skiaBytes, format, "SkiaSharp");
        AssertEncodedFormat(imageFlowBytes, format, "ImageFlow");

        using SKBitmap skiaResult = ParityFixtures.Decode(new MemoryStream(skiaBytes));
        using SKBitmap imageFlowResult = ParityFixtures.Decode(new MemoryStream(imageFlowBytes));

        Assert.True(
            skiaResult.Width == imageFlowResult.Width && skiaResult.Height == imageFlowResult.Height,
            $"Dimension mismatch for format '{format}': SkiaSharp={skiaResult.Width}x{skiaResult.Height}, " +
            $"ImageFlow={imageFlowResult.Width}x{imageFlowResult.Height}.");

        PixelSimilarity.Result similarity = PixelSimilarity.Compare(skiaResult, imageFlowResult, FormatOutlierChannelDeltaThreshold);
        _output.WriteLine($"format '{format}': {similarity}");

        AssertSimilar(format, similarity, FormatMeanChannelDeltaThreshold, FormatOutlierPixelRatioThreshold);
    }

    [Fact]
    public void SupportedFormatIntersection_IsNonEmpty()
    {
        // Sanity guard: if this ever comes back empty (e.g. a future processor drops every shared
        // format), SkiaSharpAndImageFlow_ProduceEquivalentOutput_ForEachCommonlySupportedFormat would
        // silently run zero theory cases and the format-conversion checklist item would stop being
        // covered without any test failing to say so.
        Assert.NotEmpty(SupportedFormatIntersection);
    }

    private static Task<MemoryStream> RunAsync(IImageProcessor processor, ParityCase testCase) =>
        RunAsync(processor, testCase.Command, testCase.Source);

    private static async Task<MemoryStream> RunAsync(IImageProcessor processor, ResolvedImageCommand command, SourceKind source)
    {
        using MemoryStream sourceStream = source == SourceKind.Transparent
            ? ParityFixtures.QuadrantTransparentPng()
            : ParityFixtures.QuadrantOpaquePng();

        var destination = new MemoryStream();
        await processor.ProcessAsync(sourceStream, destination, command);
        return destination;
    }

    private static void AssertEncodedFormat(byte[] bytes, string requestedFormat, string processorName)
    {
        using var stream = new MemoryStream(bytes);
        using SKCodec codec = SKCodec.Create(stream) ?? throw new InvalidOperationException(
            $"{processorName} produced output that SkiaSharp's codec could not identify for format '{requestedFormat}'.");

        SKEncodedImageFormat expected = requestedFormat.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "png" => SKEncodedImageFormat.Png,
            "webp" => SKEncodedImageFormat.Webp,
            "gif" => SKEncodedImageFormat.Gif,
            _ => throw new NotSupportedException($"Parity suite has no expected SKEncodedImageFormat mapping for '{requestedFormat}'."),
        };

        Assert.True(
            codec.EncodedFormat == expected,
            $"{processorName} was asked for '{requestedFormat}' but produced {codec.EncodedFormat}.");
    }

    private static void AssertSimilar(
        string caseName,
        PixelSimilarity.Result similarity,
        double meanChannelDeltaThreshold,
        double outlierPixelRatioThreshold)
    {
        Assert.True(
            similarity.MeanChannelDelta <= meanChannelDeltaThreshold,
            $"'{caseName}': mean per-channel delta {similarity.MeanChannelDelta:F2} exceeds threshold {meanChannelDeltaThreshold:F2}.");

        Assert.True(
            similarity.OutlierPixelRatio <= outlierPixelRatioThreshold,
            $"'{caseName}': outlier pixel ratio {similarity.OutlierPixelRatio:P1} exceeds threshold {outlierPixelRatioThreshold:P1}.");
    }
}
