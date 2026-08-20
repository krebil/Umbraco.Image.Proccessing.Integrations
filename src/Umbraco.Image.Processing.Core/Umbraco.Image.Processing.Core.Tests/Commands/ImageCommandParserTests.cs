using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Commands;

public class ImageCommandParserTests
{
    private static readonly ImageProcessingOptions Options = new() { MaxWidth = 5000, MaxHeight = 5000 };

    private static IQueryCollection Query(params (string Key, string Value)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value)));

    [Fact]
    public void EmptyQueryHasNoProcessingCommands()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query(), Options);
        Assert.False(result.HasProcessingCommands);
        Assert.True(result.AutoOrient);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("5000")]
    [InlineData("not-a-number")]
    public void DropsOutOfRangeOrInvalidWidth(string width)
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Width, width)), Options);
        Assert.Null(result.Width);
    }

    [Fact]
    public void KeepsInRangeWidth()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Width, "300")), Options);
        Assert.Equal(300, result.Width);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("150", 100)]
    [InlineData("50", 50)]
    public void ClampsQualityTo1To100(string requested, int expected)
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Quality, requested)), Options);
        Assert.Equal(expected, result.Quality);
    }

    [Fact]
    public void IgnoresUnsupportedFormat()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Format, "tiff")), Options);
        Assert.Null(result.Format);
    }

    [Fact]
    public void NormalizesSupportedFormat()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Format, "WEBP")), Options);
        Assert.Equal("webp", result.Format);
    }

    [Fact]
    public void ParsesBackgroundColor()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.BackgroundColor, "ff0000")), Options);
        Assert.Equal(new ImageColor(255, 0, 0, 255), result.BackgroundColor);
    }

    [Fact]
    public void AutoOrientDefaultsToTrueAndRespectsExplicitFalse()
    {
        Assert.True(ImageCommandParser.Parse(Query(), Options).AutoOrient);
        Assert.False(ImageCommandParser.Parse(Query((ImageProcessingCommandNames.AutoOrient, "false")), Options).AutoOrient);
    }

    [Fact]
    public void ParsesCropCoordinates()
    {
        ParsedImageCommand result = ImageCommandParser.Parse(Query((ImageProcessingCommandNames.Crop, "0.1,0.1,0.1,0.1")), Options);
        Assert.Equal(new ImageCropCoordinates(0.1f, 0.1f, 0.1f, 0.1f), result.Crop);
        Assert.True(result.HasProcessingCommands);
    }
}
