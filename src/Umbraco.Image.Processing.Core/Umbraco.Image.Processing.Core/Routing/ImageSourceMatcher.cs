using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Routing;

/// <summary>
/// Decides whether a request path is an image request and which configured source serves it: longest
/// matching <see cref="ImageSourceOptions.RoutePrefix" />, then a supported extension, then the source's
/// <see cref="ImageSourceOptions.PathPattern" />. Shared by the middleware, the URL generator and any
/// redirect-to-the-service logic, so all of them agree on which paths the service handles. A path whose
/// prefix matched but which cannot be checked (a dot segment, a pattern that times out) is
/// <see cref="ImageSourceMatchOutcome.Rejected" />, not a miss: it belongs to that source and must not be
/// passed on to a later handler.
/// </summary>
public sealed class ImageSourceMatcher
{
    /// <summary>The source used when <see cref="ImageProcessingOptions.Sources" /> is empty.</summary>
    public const string DefaultRoutePrefix = "/media";

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Route[] _routes;
    private readonly HashSet<string> _supportedExtensions;

    public ImageSourceMatcher(IOptions<ImageProcessingOptions> options)
        : this(options.Value)
    {
    }

    public ImageSourceMatcher(ImageProcessingOptions options)
    {
        _supportedExtensions = options.SupportedRequestExtensions;

        IEnumerable<ImageSourceOptions> sources = options.Sources.Count > 0
            ? options.Sources
            : [new ImageSourceOptions { RoutePrefix = DefaultRoutePrefix }];

        var routes = new List<Route>();
        foreach (ImageSourceOptions source in sources)
        {
            string prefix = NormalizeRoutePrefix(source.RoutePrefix)
                ?? throw new InvalidOperationException("Every ImageProcessing:Sources entry needs a RoutePrefix (use \"/\" to match every path).");

            if (routes.Any(r => r.Prefix == prefix))
            {
                throw new InvalidOperationException($"ImageProcessing:Sources has more than one source with the RoutePrefix \"{prefix}\".");
            }

            routes.Add(new Route(prefix, CompilePattern(prefix, source.PathPattern)));
        }

        _routes = [.. routes.OrderByDescending(r => r.Prefix.Length)];
    }

    /// <summary>The normalized route prefixes, longest first.</summary>
    public IReadOnlyList<string> RoutePrefixes => [.. _routes.Select(r => r.Prefix)];

    /// <summary>
    /// Lower-cases and gives the prefix one leading and no trailing <c>/</c>; <c>/</c> becomes
    /// <see cref="string.Empty" />. Keyed <c>IOriginalImageSource</c> registrations use the same form.
    /// </summary>
    public static string? NormalizeRoutePrefix(string? routePrefix)
    {
        if (routePrefix is null)
        {
            return null;
        }

        string trimmed = routePrefix.Trim().Trim('/');
        return trimmed.Length == 0 ? string.Empty : "/" + trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// <see cref="Match" /> reduced to "does a source serve this path": <see langword="true" /> only for
    /// <see cref="ImageSourceMatchOutcome.Matched" />. Callers that must refuse a rejected path themselves
    /// (the middleware) use <see cref="Match" />.
    /// </summary>
    public bool TryMatch(PathString path, [NotNullWhen(true)] out ImageSourceMatch? match)
    {
        match = Match(path).Match;
        return match is not null;
    }

    public ImageSourceMatchResult Match(PathString path)
    {
        Route? route = null;
        PathString remaining = default;
        foreach (Route candidate in _routes)
        {
            if (candidate.Prefix.Length == 0)
            {
                route = candidate;
                remaining = path;
                break;
            }

            if (path.StartsWithSegments(candidate.Prefix, StringComparison.OrdinalIgnoreCase, out remaining))
            {
                route = candidate;
                break;
            }
        }

        string relativePath = remaining.Value ?? string.Empty;
        if (route is null || !_supportedExtensions.Contains(Path.GetExtension(relativePath)))
        {
            return ImageSourceMatchResult.NoMatch;
        }

        if (PathSegments.HasDotSegment(relativePath))
        {
            return ImageSourceMatchResult.Rejected(route.Prefix, ImageSourceRejection.DotSegment);
        }

        if (route.Pattern is not null)
        {
            try
            {
                if (!route.Pattern.IsMatch(relativePath.TrimStart('/')))
                {
                    return ImageSourceMatchResult.NoMatch;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return ImageSourceMatchResult.Rejected(route.Prefix, ImageSourceRejection.PatternTimeout);
            }
        }

        return ImageSourceMatchResult.Matched(new ImageSourceMatch(route.Prefix, relativePath));
    }

    private static Regex? CompilePattern(string prefix, string? pathPattern)
    {
        if (string.IsNullOrEmpty(pathPattern))
        {
            return null;
        }

        try
        {
            // Anchored as a whole: the pattern is an access gate, so a partial match must never pass.
            return new Regex($"^(?:{pathPattern})$", RegexOptions.CultureInvariant, PatternTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"The PathPattern of the ImageProcessing source \"{prefix}\" is not a valid regular expression.", ex);
        }
    }

    private sealed record Route(string Prefix, Regex? Pattern);
}
