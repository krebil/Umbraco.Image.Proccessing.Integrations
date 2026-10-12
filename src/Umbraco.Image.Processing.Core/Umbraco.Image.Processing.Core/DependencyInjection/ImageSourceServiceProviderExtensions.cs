using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

public static class ImageSourceServiceProviderExtensions
{
    /// <summary>
    /// The <see cref="IOriginalImageSource" /> that serves the source at <paramref name="routePrefix" /> (as
    /// normalized by <c>ImageSourceMatcher</c>): the one registered for that prefix, or the default when the
    /// source has none of its own. The one lookup rule the middleware and an origin endpoint share.
    /// </summary>
    public static IOriginalImageSource GetOriginalImageSource(this IServiceProvider serviceProvider, string routePrefix) =>
        serviceProvider.GetKeyedService<IOriginalImageSource>(routePrefix) ?? serviceProvider.GetRequiredService<IOriginalImageSource>();
}
