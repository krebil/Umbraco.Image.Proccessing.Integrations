using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Tests.TestSupport;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;
using File = System.IO.File;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// The service refuses to start on a source configuration that would silently read the wrong place, and
/// says which source (index and prefix) is wrong. A single source keeps the 2.x defaults (LocalDisk,
/// <c>wwwroot/media</c>); with several, each source has to say where its original images come from.
/// </summary>
public sealed class SourceConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "service-source-config-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void SecondSourceWithoutStorageMode_FailsAtStartup_NamingTheSource()
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/-/archive",
            ["ImageProcessing:Sources:1:Storage:LocalDisk:RootPath"] = _root,
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/-/archive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Storage:Mode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicatePrefixes_FailAtStartup_NamingBothSources()
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/Media/",
            ["ImageProcessing:Sources:1:Storage:Mode"] = "LocalDisk",
            ["ImageProcessing:Sources:1:Storage:LocalDisk:RootPath"] = _root,
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Sources:0", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/media", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "has no RoutePrefix")]
    [InlineData("", "has no RoutePrefix")]
    [InlineData("   ", "has no RoutePrefix")]
    [InlineData("/", "root")]
    [InlineData("//", "root")]
    public void MissingOrRootRoutePrefix_FailsAtStartup_NamingTheSource(string? routePrefix, string expectedWording)
    {
        // "" is the shape an environment variable with no value takes (ImageProcessing__Sources__1__RoutePrefix=).
        var settings = new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:1:Storage:Mode"] = "LocalDisk",
            ["ImageProcessing:Sources:1:Storage:LocalDisk:RootPath"] = _root,
        };
        if (routePrefix is not null)
        {
            settings["ImageProcessing:Sources:1:RoutePrefix"] = routePrefix;
        }

        using WebApplicationFactory<Program> factory = ServiceFactory.Create(settings);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedWording, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LocalDisk", "", "", "LocalDisk:RootPath")]
    [InlineData("AzureBlob", "ContainerName", "archive", "AzureBlob:ConnectionString")]
    [InlineData("AzureBlob", "ConnectionString", "UseDevelopmentStorage=true", "AzureBlob:ContainerName")]
    [InlineData("HttpProxy", "", "", "HttpProxy:UmbracoBaseUrl")]
    public void ModeWithoutItsSettings_FailsAtStartup_NamingTheSourceAndTheSetting(string mode, string presentKey, string presentValue, string missingKey)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/-/archive",
            ["ImageProcessing:Sources:1:Storage:Mode"] = mode,
        };
        if (presentKey.Length > 0)
        {
            settings[$"ImageProcessing:Sources:1:Storage:{mode}:{presentKey}"] = presentValue;
        }

        using WebApplicationFactory<Program> factory = ServiceFactory.Create(settings);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/-/archive", ex.Message, StringComparison.Ordinal);
        Assert.Contains(missingKey, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AzureBlob", "AzureBlob:ConnectionString")]
    [InlineData("HttpProxy", "HttpProxy:UmbracoBaseUrl")]
    public void SingleSourceWithoutItsConnection_FailsAtStartup(string mode, string missingKey)
    {
        // A single source keeps the LocalDisk defaults, but a connection a mode cannot work without is
        // required regardless of how many sources there are.
        using WebApplicationFactory<Program> factory = ServiceFactory.CreateWithoutAppSettings(new Dictionary<string, string?>
        {
            ["ImageProcessing:Processor"] = "ImageFlow",
            ["ImageProcessing:Sources:0:RoutePrefix"] = "/media",
            ["ImageProcessing:Sources:0:Storage:Mode"] = mode,
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Sources:0", ex.Message, StringComparison.Ordinal);
        Assert.Contains(missingKey, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPathPattern_FailsAtStartup_NamingTheSource()
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/-/archive",
            ["ImageProcessing:Sources:1:PathPattern"] = "([a-z",
            ["ImageProcessing:Sources:1:Storage:Mode"] = "LocalDisk",
            ["ImageProcessing:Sources:1:Storage:LocalDisk:RootPath"] = _root,
        });

        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Sources:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/-/archive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PathPattern", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SingleSourceWithoutStorageMode_StartsOnLocalDisk()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(Path.Combine(_root, "photo.png"), TestImages.FourCornerPngBytes());

        await using WebApplicationFactory<Program> factory = ServiceFactory.CreateWithoutAppSettings(new Dictionary<string, string?>
        {
            ["ImageProcessing:Processor"] = "ImageFlow",
            ["ImageProcessing:Sources:0:RoutePrefix"] = "/media",
            ["ImageProcessing:Sources:0:Storage:LocalDisk:RootPath"] = _root,
            ["ImageProcessing:DerivativeCacheRootPath"] = Path.Combine(_root, "cache"),
        });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/media/photo.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void LeftoverSettingsFrom2x_AreLoggedAsWarnings()
    {
        var logs = new CapturingLoggerProvider();
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Storage:Mode"] = "AzureBlob",
            ["ImageProcessing:Proxy:UmbracoBaseUrl"] = "http://umbraco.internal",
            ["ImageProcessing:OriginalsRootPath"] = _root,
            ["ImageProcessing:RoutePrefix"] = "/media",
        }).WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));

        using HttpClient client = factory.CreateClient();

        foreach (string key in new[] { "ImageProcessing:Storage", "ImageProcessing:Proxy", "ImageProcessing:OriginalsRootPath", "ImageProcessing:RoutePrefix" })
        {
            Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(key, StringComparison.Ordinal));
        }
    }
}
