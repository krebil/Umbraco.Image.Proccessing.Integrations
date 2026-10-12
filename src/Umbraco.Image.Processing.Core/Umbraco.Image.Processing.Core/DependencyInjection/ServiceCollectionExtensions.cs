using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything processor-agnostic: options, HMAC signing, and local-disk storage. Chain a
    /// processor package's <c>UseSkiaSharp()</c>/<c>UseImageFlow()</c>/<c>UseImageSharp()</c> off the
    /// returned builder to select which <c>IImageProcessor</c> is active. In-process/standalone Umbraco
    /// consumers also chain <c>UmbracoExtensions</c>'s <c>AddUmbracoImageProcessing()</c> to register
    /// Umbraco's own <c>IImageUrlGenerator</c>/<c>IImageDimensionExtractor</c> — the standalone Service
    /// never needs it.
    /// </summary>
    public static IImageProcessingBuilder AddImageProcessing(this IServiceCollection services, Action<ImageProcessingOptions>? configure = null)
    {
        // Validated when the host starts, so an unusable source list fails there and names the source.
        services.AddOptions<ImageProcessingOptions>()
            .Configure(options => configure?.Invoke(options))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ImageProcessingOptions>, ImageProcessingOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHmacSigner, HmacSigner>();
        services.TryAddSingleton<ImageSourceMatcher>();
        services.TryAddSingleton<IOriginalImageSource>(serviceProvider =>
            new LocalDiskOriginalImageSource(serviceProvider.GetRequiredService<IOptions<ImageProcessingOptions>>().Value.OriginalsRootPath));
        services.TryAddSingleton<IDerivativeImageCache, LocalDiskDerivativeImageCache>();

        return new ImageProcessingBuilder(services);
    }
}
