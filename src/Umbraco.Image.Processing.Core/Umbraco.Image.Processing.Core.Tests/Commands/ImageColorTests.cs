using Umbraco.Image.Processing.Core.Commands;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Commands;

public class ImageColorTests
{
    [Theory]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("fff", 255, 255, 255, 255)]
    [InlineData("#f00a", 255, 0, 0, 170)]
    [InlineData("#ff0000", 255, 0, 0, 255)]
    [InlineData("112233", 0x11, 0x22, 0x33, 255)]
    [InlineData("#11223344", 0x11, 0x22, 0x33, 0x44)]
    public void ParsesValidHexForms(string value, byte r, byte g, byte b, byte a)
    {
        Assert.True(ImageColor.TryParseHex(value, out ImageColor color));
        Assert.Equal(new ImageColor(r, g, b, a), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12")]
    [InlineData("#zzzzzz")]
    [InlineData("red")]
    public void RejectsInvalidValues(string? value)
    {
        Assert.False(ImageColor.TryParseHex(value, out _));
    }
}
