using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Image.Processing.Core.Options;
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
        services.AddOptions<ImageProcessingOptions>().Configure(options => configure?.Invoke(options));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHmacSigner, HmacSigner>();
        services.TryAddSingleton<IOriginalImageSource, LocalDiskOriginalImageSource>();
        services.TryAddSingleton<IDerivativeImageCache, LocalDiskDerivativeImageCache>();

        return new ImageProcessingBuilder(services);
    }
}
