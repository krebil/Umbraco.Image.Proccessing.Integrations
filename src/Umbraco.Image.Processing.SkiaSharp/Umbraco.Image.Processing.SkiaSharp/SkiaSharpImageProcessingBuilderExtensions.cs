using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.SkiaSharp;

public static class SkiaSharpImageProcessingBuilderExtensions
{
    /// <summary>
    /// Selects the SkiaSharp <see cref="IImageProcessor" /> — the one-line swap Core's DI surface
    /// exists to enable.
    /// </summary>
    public static IImageProcessingBuilder UseSkiaSharp(this IImageProcessingBuilder builder)
    {
        builder.Services.AddSingleton<IImageProcessor, SkiaSharpImageProcessor>();
        return builder;
    }
}
