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
}
