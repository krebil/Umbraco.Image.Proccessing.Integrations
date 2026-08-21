using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Media;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;

namespace Umbraco.Image.Processing.UmbracoExtensions.DependencyInjection;

public static class UmbracoExtensionsImageProcessingBuilderExtensions
{
    /// <summary>
    /// Registers the shared <see cref="IImageUrlGenerator" />/<see cref="IImageDimensionExtractor" />
    /// implementations against Umbraco's own interfaces — needed whenever this app is an Umbraco
    /// instance (in-process or standalone image handling), irrelevant to the standalone Service.
    /// </summary>
    public static IImageProcessingBuilder AddUmbracoImageProcessing(this IImageProcessingBuilder builder)
    {
        // Replace, not TryAdd: Umbraco.Cms's own imaging package registers a default
        // IImageUrlGenerator/IImageDimensionExtractor (e.g. ImageSharpImageUrlGenerator) via a plain
        // Add, which — regardless of call order — becomes the winning registration for DI's
        // last-one-wins single-service resolution. Replace guarantees ours is the one actually used,
        // whichever order AddUmbracoImageProcessing() runs relative to CreateUmbracoBuilder().
        builder.Services.Replace(ServiceDescriptor.Singleton<IImageUrlGenerator, ImageProcessingUrlGenerator>());
        builder.Services.Replace(ServiceDescriptor.Singleton<IImageDimensionExtractor, ImageProcessingDimensionExtractor>());

        return builder;
    }
}
