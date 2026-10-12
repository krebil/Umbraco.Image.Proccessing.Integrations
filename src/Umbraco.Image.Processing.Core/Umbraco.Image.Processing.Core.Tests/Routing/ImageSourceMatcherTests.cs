using Microsoft.AspNetCore.Http;
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
    [InlineData("/media/../-/archive/photo.png", "/media")]
    [InlineData("/media/..\\-\\archive\\photo.png", "/media")]
    [InlineData("/media/1234/../../-/archive/photo.png", "/media")]
    [InlineData("/media/./photo.png", "/media")]
    [InlineData("/-/archive/./photo.png", "/-/archive")]
    public void DotSegments_AreRejected(string path, string expectedPrefix)
    {
        // Hosts that don't normalize the path (or a backslash Kestrel leaves alone) would otherwise hand
        // one source's storage a path that resolves into another's, past that source's PathPattern.
        ImageSourceMatcher matcher = Create(Source("/media"), Source("/-/archive", @"[a-z]+\.png"));

        ImageSourceMatchResult result = matcher.Match(path);

        Assert.Equal(ImageSourceMatchOutcome.Rejected, result.Outcome);
        Assert.Equal(ImageSourceRejection.DotSegment, result.Rejection);
        Assert.Equal(expectedPrefix, result.RoutePrefix);
        Assert.Null(result.Match);
        Assert.False(matcher.TryMatch(path, out _));
    }

    [Theory]
    [InlineData("/media/..%2F..%2Fsecret.png")]
    [InlineData("/media/..%2f..%2fsecret.png")]
    [InlineData("/media/..%5C..%5Csecret.png")]
    [InlineData("/media/1234%5c..%5c..%5csecret.png")]
    [InlineData("/-/archive/assets/..%2Fsecret.png")]
    public void EncodedSeparators_AreRejected(string path)
    {
        // Kestrel decodes the request path once but keeps %2F encoded, so "..%2F.." reaches the matcher
        // as one segment and HasDotSegment never sees the "..". No source path legitimately contains an
        // encoded separator, so the path is refused outright rather than trusting every storage to treat
        // it literally. A double-encoded %252F arrives here as %2F after that one decode. The PathString
        // is built explicitly: its implicit string conversion decodes like a host does (%5C becomes "\",
        // which is then a dot segment), and this test is about the value that stays encoded.
        ImageSourceMatcher matcher = Create(Source("/media"), Source("/-/archive", @"assets/[a-z]+\.png"));

        ImageSourceMatchResult result = matcher.Match(new PathString(path));

        Assert.Equal(ImageSourceMatchOutcome.Rejected, result.Outcome);
        Assert.Equal(ImageSourceRejection.EncodedSeparator, result.Rejection);
        Assert.NotNull(result.RoutePrefix);
    }

    [Theory]
    [InlineData("/other/photo.png")]
    [InlineData("/media/styles.css")]
    [InlineData("/-/archive/pages/photo.png")]
    public void PathNoSourceServes_IsNoMatch(string path)
    {
        // A prefix miss, an unsupported extension and a pattern miss all mean "not an image request":
        // the request is not ours and goes on to the next handler.
        ImageSourceMatcher matcher = Create(Source("/media"), Source("/-/archive", @"assets/[a-z]+\.png"));

        ImageSourceMatchResult result = matcher.Match(path);

        Assert.Equal(ImageSourceMatchOutcome.NoMatch, result.Outcome);
        Assert.Null(result.RoutePrefix);
        Assert.Null(result.Match);
        Assert.Null(result.Rejection);
    }

    [Fact]
    public void MatchedPath_CarriesTheSourceAndRelativePath()
    {
        ImageSourceMatcher matcher = Create(Source("/media"));

        ImageSourceMatchResult result = matcher.Match("/media/1234/photo.png");

        Assert.Equal(ImageSourceMatchOutcome.Matched, result.Outcome);
        Assert.Equal("/media", result.RoutePrefix);
        Assert.Equal(new ImageSourceMatch("/media", "/1234/photo.png"), result.Match);
        Assert.Null(result.Rejection);
    }

    [Fact]
    public void PatternTimeout_IsRejected_NotAMiss()
    {
        // (\w+\s?)* backtracks exponentially on a long run of word characters that is not followed by the
        // end of the input. The prefix matched, so the request is ours, but the gate could not be checked:
        // it must be refused rather than passed on as if no source served it.
        ImageSourceMatcher matcher = Create(Source("/media", @"(\w+\s?)*"));
        string path = "/media/" + new string('a', 64) + ".png";

        ImageSourceMatchResult result = matcher.Match(path);

        Assert.Equal(ImageSourceMatchOutcome.Rejected, result.Outcome);
        Assert.Equal(ImageSourceRejection.PatternTimeout, result.Rejection);
        Assert.Equal("/media", result.RoutePrefix);
        Assert.False(matcher.TryMatch(path, out _));
    }

    [Fact]
    public void DotsInsideASegment_AreStillMatched()
    {
        ImageSourceMatcher matcher = Create(Source("/media"));

        Assert.True(matcher.TryMatch("/media/1234/my..photo.v2.jpg", out _));
    }

    [Fact]
    public void DuplicatePrefixes_AreRejected_NamingBothSources()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Create(Source("/media"), Source("/Media/")));

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Sources:0", ex.Message, StringComparison.Ordinal);
        Assert.Contains("\"/media\"", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRoutePrefix_IsRejected_NamingTheSource()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Create(Source("/media"), Source(null)));

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("RoutePrefix", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPathPattern_IsRejected_NamingTheSource()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Create(Source("/media"), Source("/-/archive", "([a-z")));

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("\"/-/archive\"", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PathPattern", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ListsEveryProblem_AndIsEmptyForValidSources()
    {
        var invalid = new ImageProcessingOptions { Sources = [Source(null), Source("/media", "([a-z"), Source("/media")] };
        var valid = new ImageProcessingOptions { Sources = [Source("/media"), Source("/-/archive", ArchivePattern)] };

        Assert.Equal(3, ImageSourceMatcher.Validate(invalid).Count);
        Assert.Empty(ImageSourceMatcher.Validate(valid));
        Assert.Empty(ImageSourceMatcher.Validate(new ImageProcessingOptions()));
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
