using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Media;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.Core.UrlGeneration;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything processor-agnostic: options, HMAC signing, local-disk storage, and the
    /// single shared <see cref="IImageUrlGenerator" />/<see cref="IImageDimensionExtractor" />. Chain a
    /// processor package's <c>UseSkiaSharp()</c>/<c>UseImageFlow()</c>/<c>UseImageSharp()</c> off the
    /// returned builder to select which <c>IImageProcessor</c> is active.
    /// </summary>
    public static IImageProcessingBuilder AddImageProcessing(this IServiceCollection services, Action<ImageProcessingOptions>? configure = null)
    {
        services.AddOptions<ImageProcessingOptions>().Configure(options => configure?.Invoke(options));

        services.TryAddSingleton<IHmacSigner, HmacSigner>();
        services.TryAddSingleton<IOriginalImageSource, LocalDiskOriginalImageSource>();
        services.TryAddSingleton<IDerivativeImageCache, LocalDiskDerivativeImageCache>();
        services.TryAddSingleton<IImageUrlGenerator, ImageProcessingUrlGenerator>();
        services.TryAddSingleton<IImageDimensionExtractor, ImageProcessingDimensionExtractor>();

        return new ImageProcessingBuilder(services);
    }
}
