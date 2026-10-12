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
/// prefix matched but which cannot be checked (a dot segment, an encoded separator, a pattern that times out) is
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
        _routes = BuildRoutes(options, out List<string> problems);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }
    }

    /// <summary>The normalized route prefixes, longest first.</summary>
    public IReadOnlyList<string> RoutePrefixes => [.. _routes.Select(r => r.Prefix)];

    /// <summary>
    /// What stops <see cref="ImageProcessingOptions.Sources" /> from being matched against, one message per
    /// source naming its index and prefix: a missing RoutePrefix, a prefix another source already has, or a
    /// PathPattern that is not a valid regular expression. Empty when the sources are valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(ImageProcessingOptions options)
    {
        BuildRoutes(options, out List<string> problems);
        return problems;
    }

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

        if (PathSegments.HasEncodedSeparator(relativePath))
        {
            return ImageSourceMatchResult.Rejected(route.Prefix, ImageSourceRejection.EncodedSeparator);
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

    private static Route[] BuildRoutes(ImageProcessingOptions options, out List<string> problems)
    {
        problems = [];
        IReadOnlyList<ImageSourceOptions> sources = options.Sources.Count > 0
            ? options.Sources
            : [new ImageSourceOptions { RoutePrefix = DefaultRoutePrefix }];

        var routes = new List<Route>();
        var indexByPrefix = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < sources.Count; index++)
        {
            ImageSourceOptions source = sources[index];
            string? prefix = NormalizeRoutePrefix(source.RoutePrefix);
            if (prefix is null)
            {
                problems.Add($"ImageProcessing:Sources:{index} has no RoutePrefix (use \"/\" to match every path).");
                continue;
            }

            if (indexByPrefix.TryGetValue(prefix, out int twin))
            {
                problems.Add($"ImageProcessing:Sources:{index} (\"{prefix}\") has the same RoutePrefix as ImageProcessing:Sources:{twin}.");
                continue;
            }

            indexByPrefix[prefix] = index;

            Regex? pattern = null;
            if (!string.IsNullOrEmpty(source.PathPattern))
            {
                try
                {
                    // Anchored as a whole: the pattern is an access gate, so a partial match must never pass.
                    pattern = new Regex($"^(?:{source.PathPattern})$", RegexOptions.CultureInvariant, PatternTimeout);
                }
                catch (ArgumentException ex)
                {
                    problems.Add($"ImageProcessing:Sources:{index} (\"{prefix}\"): PathPattern is not a valid regular expression: {ex.Message}");
                    continue;
                }
            }

            routes.Add(new Route(prefix, pattern));
        }

        return [.. routes.OrderByDescending(r => r.Prefix.Length)];
    }

    private sealed record Route(string Prefix, Regex? Pattern);
}
