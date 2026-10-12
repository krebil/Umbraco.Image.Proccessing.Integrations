namespace Umbraco.Image.Processing.Core.Routing;

public enum ImageSourceMatchOutcome
{
    /// <summary>No source serves the path: no prefix matched, the extension is not an image's, or the source's PathPattern missed.</summary>
    NoMatch,

    /// <summary>A source serves the path; <see cref="ImageSourceMatchResult.Match" /> says which.</summary>
    Matched,

    /// <summary>A source's prefix matched, but the path could not or must not be checked. The request is that source's and has to be refused, never passed on.</summary>
    Rejected,
}

public enum ImageSourceRejection
{
    /// <summary>The path after the prefix has a <c>.</c> or <c>..</c> segment.</summary>
    DotSegment,

    /// <summary>The source's PathPattern did not finish within its timeout.</summary>
    PatternTimeout,
}

/// <summary>
/// What <see cref="ImageSourceMatcher.Match" /> decided for a request path. <see cref="RoutePrefix" /> is set
/// whenever a source's prefix matched, so a refusal can be attributed to its source.
/// </summary>
public sealed record ImageSourceMatchResult
{
    public static readonly ImageSourceMatchResult NoMatch = new(ImageSourceMatchOutcome.NoMatch, null, null, null);

    private ImageSourceMatchResult(ImageSourceMatchOutcome outcome, string? routePrefix, ImageSourceMatch? match, ImageSourceRejection? rejection)
    {
        Outcome = outcome;
        RoutePrefix = routePrefix;
        Match = match;
        Rejection = rejection;
    }

    public ImageSourceMatchOutcome Outcome { get; }

    public string? RoutePrefix { get; }

    /// <summary>Set only when <see cref="Outcome" /> is <see cref="ImageSourceMatchOutcome.Matched" />.</summary>
    public ImageSourceMatch? Match { get; }

    /// <summary>Set only when <see cref="Outcome" /> is <see cref="ImageSourceMatchOutcome.Rejected" />.</summary>
    public ImageSourceRejection? Rejection { get; }

    public static ImageSourceMatchResult Matched(ImageSourceMatch match) =>
        new(ImageSourceMatchOutcome.Matched, match.RoutePrefix, match, null);

    public static ImageSourceMatchResult Rejected(string routePrefix, ImageSourceRejection rejection) =>
        new(ImageSourceMatchOutcome.Rejected, routePrefix, null, rejection);
}
