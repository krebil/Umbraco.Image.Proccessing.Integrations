using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Processing;

public class ExifOrientationTransformTests
{
    [Theory]
    [InlineData(ExifOrientation.Unknown, false)]
    [InlineData(ExifOrientation.TopLeft, false)]
    [InlineData(ExifOrientation.TopRight, false)]
    [InlineData(ExifOrientation.BottomRight, false)]
    [InlineData(ExifOrientation.BottomLeft, false)]
    [InlineData(ExifOrientation.LeftTop, true)]
    [InlineData(ExifOrientation.RightTop, true)]
    [InlineData(ExifOrientation.RightBottom, true)]
    [InlineData(ExifOrientation.LeftBottom, true)]
    public void IsRotated_OnlyTrueForTheFourNinetyDegreeVariants(ushort orientation, bool expected)
    {
        Assert.Equal(expected, ExifOrientationTransform.IsRotated(orientation));
    }
}
