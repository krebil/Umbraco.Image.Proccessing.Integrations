using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Processing;

/// <summary>
/// All expected values below are hand-derived from ImageSharp's own
/// <c>ResizeHelper.CalculateTargetLocationAndBounds</c> algorithm (see
/// <see cref="ResizeCalculator" />'s doc comment for the source), restated in this package's
/// source-space-crop / canvas-space-placement shape rather than ImageSharp's own
/// intermediate-canvas-space numbers — the two are mathematically equivalent framings, not a
/// reproduction of ImageSharp's exact intermediate arithmetic.
/// </summary>
public class ResizeCalculatorTests
{
    [Fact]
    public void NoDimensionsRequested_ReturnsSourceSizeUnchanged()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, null, null, ResizeMode.Crop);

        Assert.Equal(new ResizeResolution(100, 50, null, null), result);
    }

    [Fact]
    public void WidthOnly_PreservesAspectRatio()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 200, null, ResizeMode.Crop);

        Assert.Equal(200, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void HeightOnly_PreservesAspectRatio()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, null, 150, ResizeMode.Crop);

        Assert.Equal(300, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    public void Crop_SquareTargetFromWideSource_CropsCenteredOverflow()
    {
        // 100x50 source, 60x60 (square) target: scales to cover (1.2x, matching height), then crops
        // the now-120-wide-equivalent source down to a centered 50-wide source-space window.
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Crop);

        Assert.Equal(60, result.Width);
        Assert.Equal(60, result.Height);
        Assert.Equal(new CropRectangle(25, 0, 50, 50), result.Crop);
        Assert.Null(result.Placement);
    }

    [Fact]
    public void Pad_MismatchedAspect_LettersboxesShorterAxis()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Pad);

        Assert.Equal(60, result.Width);
        Assert.Equal(60, result.Height);
        Assert.Null(result.Crop);
        Assert.Equal(new CropRectangle(0, 15, 60, 30), result.Placement);
    }

    [Fact]
    public void BoxPad_SourceSmallerThanTarget_CentersUnscaled()
    {
        ResizeResolution result = ResizeCalculator.Resolve(20, 10, 60, 60, ResizeMode.BoxPad);

        Assert.Equal(60, result.Width);
        Assert.Equal(60, result.Height);
        Assert.Null(result.Crop);
        Assert.Equal(new CropRectangle(20, 25, 20, 10), result.Placement);
    }

    [Fact]
    public void BoxPad_SourceLargerThanTarget_BehavesLikePad()
    {
        ResizeResolution boxPad = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.BoxPad);
        ResizeResolution pad = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Pad);

        Assert.Equal(pad, boxPad);
    }

    [Fact]
    public void Max_FitsWithinPreservingAspect_OutputIsFittedSizeNotRequestedSize()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Max);

        Assert.Equal(60, result.Width);
        Assert.Equal(30, result.Height);
        Assert.Null(result.Crop);
        Assert.Null(result.Placement);
    }

    [Fact]
    public void Min_RequestWithinSourceBounds_DownscalesPreservingAspect()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 60, 20, ResizeMode.Min);

        Assert.Equal(40, result.Width);
        Assert.Equal(20, result.Height);
        Assert.Null(result.Crop);
        Assert.Null(result.Placement);
    }

    [Fact]
    public void Min_RequestLargerThanSource_NeverUpscales()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 200, 200, ResizeMode.Min);

        Assert.Equal(new ResizeResolution(100, 50, null, null), result);
    }

    [Fact]
    public void Stretch_DistortsToExactRequestedSize()
    {
        ResizeResolution result = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Stretch);

        Assert.Equal(new ResizeResolution(60, 60, null, null), result);
    }

    [Fact]
    public void Manual_BehavesIdenticallyToStretch()
    {
        // Manual's TargetRectangle is only ever settable via the raw ImageSharp API, never via a URL —
        // ImageSharp.Web's own query-string surface always resolves it to (0,0,width,height), which is
        // exactly Stretch. See ResizeCalculator's doc comment.
        ResizeResolution manual = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Manual);
        ResizeResolution stretch = ResizeCalculator.Resolve(100, 50, 60, 60, ResizeMode.Stretch);

        Assert.Equal(stretch, manual);
    }
}
