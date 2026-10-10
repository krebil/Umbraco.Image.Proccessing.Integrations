using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Routing;

public class ImageSourceMatcherTests
{
    private const string ArchivePattern = @"^[0-9a-f]{32}/assets/[0-9a-f]{32}\.[a-z0-9]{1,10}$";
    private const string Hex32 = "0123456789abcdef0123456789abcdef";

    private static ImageSourceMatcher Create(params ImageSourceOptions[] sources) =>
        new(new ImageProcessingOptions { Sources = [.. sources] });

    private static ImageSourceOptions Source(string? routePrefix, string? pathPattern = null) =>
        new() { RoutePrefix = routePrefix, PathPattern = pathPattern };

    [Fact]
    public void NoSourcesConfigured_FallsBackToASingleMediaSource()
    {
        ImageSourceMatcher matcher = Create();

        Assert.True(matcher.TryMatch("/media/1234/photo.jpg", out ImageSourceMatch? match));
        Assert.Equal("/media", match.RoutePrefix);
        Assert.Equal("/1234/photo.jpg", match.RelativePath);
        Assert.False(matcher.TryMatch("/other/photo.jpg", out _));
    }

    [Fact]
    public void PicksTheLongestMatchingPrefix()
    {
        ImageSourceMatcher matcher = Create(Source("/media"), Source("/media/archive"));

        Assert.True(matcher.TryMatch("/media/archive/photo.jpg", out ImageSourceMatch? archive));
        Assert.Equal("/media/archive", archive.RoutePrefix);
        Assert.Equal("/photo.jpg", archive.RelativePath);

        Assert.True(matcher.TryMatch("/media/1234/photo.jpg", out ImageSourceMatch? media));
        Assert.Equal("/media", media.RoutePrefix);
    }

    [Fact]
    public void PrefixMatchesWholeSegmentsOnly()
    {
        ImageSourceMatcher matcher = Create(Source("/media"));

        Assert.False(matcher.TryMatch("/mediaextra/photo.jpg", out _));
    }

    [Fact]
    public void PrefixIsNormalizedAndCaseInsensitive()
    {
        ImageSourceMatcher matcher = Create(Source("media/"));

        Assert.True(matcher.TryMatch("/MEDIA/photo.jpg", out ImageSourceMatch? match));
        Assert.Equal("/media", match.RoutePrefix);
        Assert.Equal("/photo.jpg", match.RelativePath);
    }

    [Fact]
    public void EmptyPrefix_MatchesEverythingButLosesToALongerPrefix()
    {
        ImageSourceMatcher matcher = Create(Source(""), Source("/-/archive"));

        Assert.True(matcher.TryMatch("/anything/photo.jpg", out ImageSourceMatch? root));
        Assert.Equal(string.Empty, root.RoutePrefix);
        Assert.Equal("/anything/photo.jpg", root.RelativePath);

        Assert.True(matcher.TryMatch("/-/archive/photo.jpg", out ImageSourceMatch? archive));
        Assert.Equal("/-/archive", archive.RoutePrefix);
    }

    [Fact]
    public void UnsupportedExtension_IsNotAnImageRequest()
    {
        ImageSourceMatcher matcher = Create(Source("/media"));

        Assert.False(matcher.TryMatch("/media/styles.css", out _));
    }

    [Fact]
    public void PathPattern_GatesThePathAfterThePrefix()
    {
        ImageSourceMatcher matcher = Create(Source("/-/archive", ArchivePattern));

        Assert.True(matcher.TryMatch($"/-/archive/{Hex32}/assets/{Hex32}.png", out ImageSourceMatch? match));
        Assert.Equal($"/{Hex32}/assets/{Hex32}.png", match.RelativePath);

        Assert.False(matcher.TryMatch($"/-/archive/{Hex32}/pages/{Hex32}.png", out _));
        Assert.False(matcher.TryMatch("/-/archive/photo.png", out _));
    }

    [Fact]
    public void PathPattern_MustMatchTheWholePath()
    {
        ImageSourceMatcher matcher = Create(Source("/-/archive", @"assets/[a-z]+\.png"));

        Assert.True(matcher.TryMatch("/-/archive/assets/photo.png", out _));
        Assert.False(matcher.TryMatch("/-/archive/secret/assets/photo.png", out _));
        Assert.False(matcher.TryMatch("/-/archive/assets/photo.png.png", out _));
    }

    [Fact]
    public void PathPatternMiss_DoesNotFallThroughToAShorterPrefix()
    {
        ImageSourceMatcher matcher = Create(Source(""), Source("/-/archive", ArchivePattern));

        Assert.False(matcher.TryMatch("/-/archive/photo.png", out _));
    }

    [Theory]
    [InlineData("/media/../-/archive/photo.png")]
    [InlineData("/media/..\\-\\archive\\photo.png")]
    [InlineData("/media/1234/../../-/archive/photo.png")]
    [InlineData("/media/./photo.png")]
    [InlineData("/-/archive/./photo.png")]
    public void DotSegments_AreNotMatched(string path)
    {
        // Hosts that don't normalize the path (or a backslash Kestrel leaves alone) would otherwise hand
        // one source's storage a path that resolves into another's, past that source's PathPattern.
        ImageSourceMatcher matcher = Create(Source("/media"), Source("/-/archive", @"[a-z]+\.png"));

        Assert.False(matcher.TryMatch(path, out _));
    }

    [Fact]
    public void DotsInsideASegment_AreStillMatched()
    {
        ImageSourceMatcher matcher = Create(Source("/media"));

        Assert.True(matcher.TryMatch("/media/1234/my..photo.v2.jpg", out _));
    }

    [Fact]
    public void DuplicatePrefixes_AreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Create(Source("/media"), Source("/Media/")));
    }

    [Fact]
    public void MissingRoutePrefix_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Create(Source(null)));
    }

    [Fact]
    public void InvalidPathPattern_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Create(Source("/media", "([a-z")));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("media", "/media")]
    [InlineData("/media/", "/media")]
    [InlineData("/-/Archive", "/-/archive")]
    public void NormalizeRoutePrefix(string? input, string? expected)
    {
        Assert.Equal(expected, ImageSourceMatcher.NormalizeRoutePrefix(input));
    }
}
