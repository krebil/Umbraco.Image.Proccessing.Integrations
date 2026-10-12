namespace Umbraco.Image.Processing.Core.Routing;

/// <summary>
/// The source a request path resolved to: its normalized <paramref name="RoutePrefix" /> (also the key its
/// <c>IOriginalImageSource</c> is registered under) and the <paramref name="RelativePath" /> after it.
/// </summary>
public sealed record ImageSourceMatch(string RoutePrefix, string RelativePath);
