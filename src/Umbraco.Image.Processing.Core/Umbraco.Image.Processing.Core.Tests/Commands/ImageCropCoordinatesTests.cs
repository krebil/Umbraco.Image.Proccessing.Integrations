using Umbraco.Image.Processing.Core.Commands;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Commands;

public class ImageCropCoordinatesTests
{
    [Fact]
    public void ParsesFourCommaSeparatedValues()
    {
        Assert.True(ImageCropCoordinates.TryParse("0.1,0.2,0.3,0.4", out ImageCropCoordinates coordinates));
        Assert.Equal(new ImageCropCoordinates(0.1f, 0.2f, 0.3f, 0.4f), coordinates);
    }

    [Fact]
    public void RejectsAllZero()
    {
        Assert.False(ImageCropCoordinates.TryParse("0,0,0,0", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0.1,0.2,0.3")]
    [InlineData("0.1,0.2,0.3,0.4,0.5")]
    [InlineData("a,b,c,d")]
    public void RejectsMalformedValues(string? value)
    {
        Assert.False(ImageCropCoordinates.TryParse(value, out _));
    }
}
