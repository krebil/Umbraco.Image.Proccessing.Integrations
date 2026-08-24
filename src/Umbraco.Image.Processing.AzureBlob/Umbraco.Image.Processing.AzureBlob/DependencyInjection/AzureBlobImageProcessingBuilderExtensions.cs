using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.AzureBlob.Storage;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.AzureBlob.DependencyInjection;

public static class AzureBlobImageProcessingBuilderExtensions
{
    /// <summary>
    /// Swaps the derivative cache backend from Core's default (local disk) to Azure Blob Storage, so
    /// multiple standalone Service instances behind a load balancer can share one cache.
    /// </summary>
    public static IImageProcessingBuilder UseAzureBlobDerivativeCache(this IImageProcessingBuilder builder, Action<AzureBlobCacheOptions> configure)
    {
        builder.Services.AddOptions<AzureBlobCacheOptions>().Configure(configure);

        // Replace, not TryAdd: AddImageProcessing() already registers LocalDiskDerivativeImageCache
        // as the default via TryAddSingleton, regardless of call order relative to this method.
        builder.Services.Replace(ServiceDescriptor.Singleton<IDerivativeImageCache, AzureBlobDerivativeImageCache>());

        return builder;
    }

    /// <summary>
    /// Swaps the original-image source from Core's default (local disk) to Azure Blob Storage, so the
    /// standalone Service can resolve media directly from the same container Umbraco's own Blob-backed
    /// media file system writes to, without a shared disk/volume between the two.
    /// </summary>
    public static IImageProcessingBuilder UseAzureBlobOriginalImageSource(this IImageProcessingBuilder builder, Action<AzureBlobOriginalImageSourceOptions> configure)
    {
        builder.Services.AddOptions<AzureBlobOriginalImageSourceOptions>().Configure(configure);

        // Replace, not TryAdd: AddImageProcessing() already registers LocalDiskOriginalImageSource
        // as the default via TryAddSingleton, regardless of call order relative to this method.
        builder.Services.Replace(ServiceDescriptor.Singleton<IOriginalImageSource, AzureBlobOriginalImageSource>());

        return builder;
    }
}
