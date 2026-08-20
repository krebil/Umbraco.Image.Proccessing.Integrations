using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.ImageFlow.Tests;

public class ImageFlowImageProcessingBuilderExtensionsTests
{
    [Fact]
    public void UseImageFlow_RegistersImageFlowImageProcessor()
    {
        var services = new ServiceCollection();
        var builder = new TestImageProcessingBuilder(services);

        IImageProcessingBuilder result = builder.UseImageFlow();

        ServiceProvider provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IImageProcessor>();
        Assert.IsType<ImageFlowImageProcessor>(processor);
        Assert.Same(builder, result);
    }

    private sealed class TestImageProcessingBuilder(IServiceCollection services) : IImageProcessingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
