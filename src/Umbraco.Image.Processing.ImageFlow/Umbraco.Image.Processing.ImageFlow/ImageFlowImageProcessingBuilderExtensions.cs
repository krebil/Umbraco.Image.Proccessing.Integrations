using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.ImageFlow;

public static class ImageFlowImageProcessingBuilderExtensions
{
    /// <summary>
    /// Selects the ImageFlow <see cref="IImageProcessor" /> — the one-line swap Core's DI surface
    /// exists to enable.
    /// </summary>
    public static IImageProcessingBuilder UseImageFlow(this IImageProcessingBuilder builder)
    {
        builder.Services.AddSingleton<IImageProcessor, ImageFlowImageProcessor>();
        return builder;
    }
}
