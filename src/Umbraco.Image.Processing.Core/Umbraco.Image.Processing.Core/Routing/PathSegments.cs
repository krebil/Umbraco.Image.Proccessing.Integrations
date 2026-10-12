namespace Umbraco.Image.Processing.Core.Routing;

public static class PathSegments
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// True when <paramref name="path" /> has a <c>.</c> or <c>..</c> segment, split on both <c>/</c> and
    /// <c>\</c>: such a path can resolve outside the storage root or prefix it was meant for.
    /// </summary>
    public static bool HasDotSegment(string path) =>
        path.Split(Separators).Any(segment => segment is "." or "..");

    /// <summary>
    /// True when <paramref name="path" /> still contains a percent-encoded <c>/</c> or <c>\</c>. Hosts decode
    /// the request path once but keep <c>%2F</c> encoded, so <c>..%2F..</c> is one segment to
    /// <see cref="HasDotSegment" />; a storage that decoded it would see a real <c>..</c>. No image path
    /// legitimately contains an encoded separator, so such a path is refused outright.
    /// </summary>
    public static bool HasEncodedSeparator(string path) =>
        path.Contains("%2F", StringComparison.OrdinalIgnoreCase) || path.Contains("%5C", StringComparison.OrdinalIgnoreCase);
}
