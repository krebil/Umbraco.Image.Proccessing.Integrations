using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Options;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.DependencyInjection;

/// <summary>
/// A host with an unusable source list fails when it starts, naming the source, rather than when the
/// middleware is first built or a request first arrives.
/// </summary>
public sealed class ImageProcessingOptionsValidationTests
{
    [Fact]
    public async Task InvalidSources_FailTheHostAtStart_NamingTheSource()
    {
        using IHost host = new HostBuilder()
            .ConfigureServices(services => services.AddImageProcessing(options =>
                options.Sources = [new ImageSourceOptions { RoutePrefix = "/media" }, new ImageSourceOptions { RoutePrefix = "/-/archive", PathPattern = "([a-z" }]))
            .Build();

        OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("\"/-/archive\"", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "has no RoutePrefix")]
    [InlineData("/", "root")]
    public async Task EmptyOrRootPrefix_FailsTheHostAtStart_NamingTheSource(string routePrefix, string expectedWording)
    {
        // "" is what an empty environment variable binds to: it must not become a catch-all source.
        using IHost host = new HostBuilder()
            .ConfigureServices(services => services.AddImageProcessing(options =>
                options.Sources = [new ImageSourceOptions { RoutePrefix = "/media" }, new ImageSourceOptions { RoutePrefix = routePrefix }]))
            .Build();

        OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedWording, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidSources_StartTheHost()
    {
        using IHost host = new HostBuilder()
            .ConfigureServices(services => services.AddImageProcessing(options =>
                options.Sources = [new ImageSourceOptions { RoutePrefix = "/media" }, new ImageSourceOptions { RoutePrefix = "/-/archive", PathPattern = @"[a-z]+\.png" }]))
            .Build();

        await host.StartAsync();
        await host.StopAsync();
    }
}
