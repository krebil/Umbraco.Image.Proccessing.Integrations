using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

internal sealed class ImageProcessingBuilder(IServiceCollection services) : IImageProcessingBuilder
{
    public IServiceCollection Services { get; } = services;
}
