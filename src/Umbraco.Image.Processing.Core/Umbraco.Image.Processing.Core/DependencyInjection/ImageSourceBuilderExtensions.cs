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
    /// As <see cref="AddOriginalImageSource(IImageProcessingBuilder, string, Func{IServiceProvider, IOriginalImageSource})" />,
    /// for a storage configured through its own options type: <paramref name="configure" /> fills a fresh
    /// <typeparamref name="TOptions" />, which <paramref name="factory" /> then builds the storage from. The
    /// shape every <c>Use…OriginalImageSource(routePrefix, …)</c> overload shares.
    /// </summary>
    public static IImageProcessingBuilder AddOriginalImageSource<TOptions>(
        this IImageProcessingBuilder builder,
        string routePrefix,
        Action<TOptions> configure,
        Func<IServiceProvider, TOptions, IOriginalImageSource> factory)
        where TOptions : class, new()
    {
        var options = new TOptions();
        configure(options);
        return builder.AddOriginalImageSource(routePrefix, serviceProvider => factory(serviceProvider, options));
    }

    /// <summary>
    /// Reads the original images of the source at <paramref name="routePrefix" /> from a local directory.
    /// </summary>
    public static IImageProcessingBuilder UseLocalDiskOriginalImageSource(this IImageProcessingBuilder builder, string routePrefix, Action<LocalDiskOriginalImageSourceOptions> configure) =>
        builder.AddOriginalImageSource(routePrefix, configure, (_, options) => new LocalDiskOriginalImageSource(options.RootPath));

    /// <summary>
    /// The service key an <see cref="IOriginalImageSource" /> for <paramref name="routePrefix" /> is registered
    /// under. A missing or root prefix names no source and is refused.
    /// </summary>
    public static string KeyFor(string routePrefix) =>
        ImageSourceMatcher.NormalizeRoutePrefix(routePrefix, out string? problem)
        ?? throw new ArgumentException($"The source {problem}", nameof(routePrefix));
}
