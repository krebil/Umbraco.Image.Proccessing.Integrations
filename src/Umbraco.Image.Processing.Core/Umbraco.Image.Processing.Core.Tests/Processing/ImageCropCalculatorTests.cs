using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Processing;

public class ImageCropCalculatorTests
{
    // left=0.1, top=0.2, right-distance=0.3 (edge at 0.7), bottom-distance=0.05 (edge at 0.95).
    private static readonly ImageCropCoordinates AsymmetricCrop = new(0.1f, 0.2f, 0.3f, 0.05f);

    [Fact]
    public void NoOrientation_CropsDirectlyFromNormalizedCoordinates()
    {
        CropRectangle rect = ImageCropCalculator.Compute(AsymmetricCrop, imageWidth: 1000, imageHeight: 800, ExifOrientation.TopLeft);

        Assert.Equal(new CropRectangle(100, 160, 600, 600), rect);
    }

    [Fact]
    public void TopRight_MirrorsHorizontally()
    {
        CropRectangle rect = ImageCropCalculator.Compute(AsymmetricCrop, imageWidth: 1000, imageHeight: 800, ExifOrientation.TopRight);

        // Same margins, flipped left/right: the crop now sits 300px from the left instead of 100px.
        Assert.Equal(new CropRectangle(300, 160, 600, 600), rect);
    }

    [Fact]
    public void LeftTop_SwapsAxes()
    {
        CropRectangle rect = ImageCropCalculator.Compute(AsymmetricCrop, imageWidth: 1000, imageHeight: 800, ExifOrientation.LeftTop);

        Assert.Equal(new CropRectangle(200, 80, 750, 480), rect);
    }

    [Fact]
    public void ClampsOutOfRangeCoordinates()
    {
        var coordinates = new ImageCropCoordinates(-1f, -1f, -1f, -1f);
        CropRectangle rect = ImageCropCalculator.Compute(coordinates, imageWidth: 100, imageHeight: 100, ExifOrientation.TopLeft);

        Assert.Equal(new CropRectangle(0, 0, 100, 100), rect);
    }
}
