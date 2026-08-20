using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.SkiaSharp.Tests;

public class SkiaSharpImageProcessingBuilderExtensionsTests
{
    [Fact]
    public void UseSkiaSharp_RegistersSkiaSharpImageProcessor()
    {
        var services = new ServiceCollection();
        var builder = new TestImageProcessingBuilder(services);

        IImageProcessingBuilder result = builder.UseSkiaSharp();

        ServiceProvider provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IImageProcessor>();
        Assert.IsType<SkiaSharpImageProcessor>(processor);
        Assert.Same(builder, result);
    }

    private sealed class TestImageProcessingBuilder(IServiceCollection services) : IImageProcessingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
