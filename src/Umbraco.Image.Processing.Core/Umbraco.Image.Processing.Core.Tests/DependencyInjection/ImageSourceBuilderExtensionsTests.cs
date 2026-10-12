using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Storage;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.DependencyInjection;

public sealed class ImageSourceBuilderExtensionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "source-registration-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void MissingOrRootPrefix_CannotBeRegistered(string? routePrefix)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddImageProcessing().UseLocalDiskOriginalImageSource(routePrefix!, o => o.RootPath = _root));
        Assert.Throws<ArgumentException>(() => ImageSourceBuilderExtensions.KeyFor(routePrefix!));
    }

    [Fact]
    public async Task LocalDiskSource_IsRegisteredUnderItsNormalizedPrefix_WithTheConfiguredRoot()
    {
        string mediaRoot = Path.Combine(_root, "media");
        Directory.CreateDirectory(mediaRoot);
        await File.WriteAllBytesAsync(Path.Combine(mediaRoot, "photo.jpg"), [1, 2, 3]);

        var services = new ServiceCollection();
        services.AddImageProcessing().UseLocalDiskOriginalImageSource("/Media/", o => o.RootPath = mediaRoot);
        using ServiceProvider provider = services.BuildServiceProvider();

        IOriginalImageSource source = provider.GetRequiredKeyedService<IOriginalImageSource>("/media");

        Assert.IsType<LocalDiskOriginalImageSource>(source);
        Assert.NotNull(await source.OpenReadAsync("/photo.jpg"));
        Assert.IsType<LocalDiskOriginalImageSource>(provider.GetRequiredService<IOriginalImageSource>());
    }

    [Fact]
    public void OptionsOverload_ConfiguresFreshOptions_AndHandsThemToTheFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton("a dependency");
        FakeOptions? seen = null;
        string? dependency = null;

        services.AddImageProcessing().AddOriginalImageSource<FakeOptions>(
            "/-/archive",
            o => o.Name = "configured",
            (serviceProvider, o) =>
            {
                seen = o;
                dependency = serviceProvider.GetRequiredService<string>();
                return new FakeSource();
            });
        using ServiceProvider provider = services.BuildServiceProvider();

        IOriginalImageSource source = provider.GetRequiredKeyedService<IOriginalImageSource>("/-/archive");

        Assert.IsType<FakeSource>(source);
        Assert.Equal("configured", seen?.Name);
        Assert.Equal("a dependency", dependency);
        Assert.Same(source, provider.GetRequiredKeyedService<IOriginalImageSource>("/-/archive"));
    }

    private sealed class FakeOptions
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FakeSource : IOriginalImageSource
    {
        public Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
    }
}
