using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

public static class ImageSourceBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IOriginalImageSource" /> for the source whose
    /// <see cref="ImageSourceOptions.RoutePrefix" /> is <paramref name="routePrefix" />. The route itself is
    /// declared in <see cref="ImageProcessingOptions.Sources" />; a declared source without a registration
    /// here reads from the default <see cref="IOriginalImageSource" />.
    /// </summary>
    public static IImageProcessingBuilder AddOriginalImageSource(this IImageProcessingBuilder builder, string routePrefix, Func<IServiceProvider, IOriginalImageSource> factory)
    {
        builder.Services.AddKeyedSingleton(KeyFor(routePrefix), (serviceProvider, _) => factory(serviceProvider));
        return builder;
    }

    /// <summary>
    /// Reads the originals of the source at <paramref name="routePrefix" /> from a local directory.
    /// </summary>
    public static IImageProcessingBuilder UseLocalDiskOriginalImageSource(this IImageProcessingBuilder builder, string routePrefix, Action<LocalDiskOriginalImageSourceOptions> configure)
    {
        var options = new LocalDiskOriginalImageSourceOptions();
        configure(options);
        return builder.AddOriginalImageSource(routePrefix, _ => new LocalDiskOriginalImageSource(options.RootPath));
    }

    /// <summary>The service key an <see cref="IOriginalImageSource" /> for <paramref name="routePrefix" /> is registered under.</summary>
    public static string KeyFor(string routePrefix) =>
        ImageSourceMatcher.NormalizeRoutePrefix(routePrefix) ?? throw new ArgumentNullException(nameof(routePrefix));
}
