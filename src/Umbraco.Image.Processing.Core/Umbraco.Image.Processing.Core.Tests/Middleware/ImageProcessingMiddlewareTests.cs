using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Processing;
using Umbraco.Image.Processing.Core.Tests.TestSupport;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Middleware;

/// <summary>
/// What the middleware does with each <see cref="Routing.ImageSourceMatcher" /> outcome, mounted the way an
/// in-process host mounts it: ahead of static files that would otherwise serve the raw file under
/// <c>wwwroot/&lt;prefix&gt;</c>. The original-image storage and the static web root hold different bytes
/// for the same path, so the response body says which of the two answered.
/// </summary>
public sealed class ImageProcessingMiddlewareTests : IDisposable
{
    /// <summary>Backtracks exponentially on a run of word characters that is not followed by the end of the input.</summary>
    private const string CatastrophicPattern = @"(\w+\s?)*";
    private static readonly byte[] OriginalBytes = "served by the middleware"u8.ToArray();
    private static readonly byte[] StaticBytes = "served by static files"u8.ToArray();
    private static readonly string LongName = new string('a', 64) + ".png";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "middleware-tests-" + Guid.NewGuid().ToString("N"));

    public ImageProcessingMiddlewareTests()
    {
        foreach (string name in new[] { "photo.png", LongName })
        {
            Write(Path.Combine(_root, "originals", name), OriginalBytes);
            Write(Path.Combine(_root, "wwwroot", "media", name), StaticBytes);
        }

        Write(Path.Combine(_root, "wwwroot", "media", "styles.css"), StaticBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Matched_IsServedFromTheSourcesOriginalImageStorage()
    {
        await using Host host = await StartAsync(pathPattern: null);

        using HttpResponseMessage response = await host.Client.GetAsync("/media/photo.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OriginalBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task NoMatch_IsPassedOn()
    {
        await using Host host = await StartAsync(pathPattern: null);

        using HttpResponseMessage response = await host.Client.GetAsync("/media/styles.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StaticBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PatternMiss_IsPassedOn()
    {
        await using Host host = await StartAsync(pathPattern: @"assets/[a-z]+\.png");

        using HttpResponseMessage response = await host.Client.GetAsync("/media/photo.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StaticBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DotSegment_IsRefusedWith400_AndNeverReachesStaticFiles()
    {
        await using Host host = await StartAsync(pathPattern: null);

        // Sent percent-encoded: System.Uri would turn a literal backslash into "/" and collapse the ".."
        // before the request leaves the client. The host decodes %5C to "\", which the matcher splits on.
        using HttpResponseMessage response = await host.Client.GetAsync("/media/..%5Cmedia%5Cphoto.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains(host.Logs.Entries, e => e.Category == typeof(ImageProcessingMiddleware).FullName && e.Message.Contains("DotSegment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PatternTimeout_IsRefusedWith404_LoggedAsWarning_AndNeverReachesStaticFiles()
    {
        await using Host host = await StartAsync(CatastrophicPattern);

        using HttpResponseMessage response = await host.Client.GetAsync($"/media/{LongName}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        (LogLevel Level, string Category, string Message) entry = Assert.Single(host.Logs.Entries, e => e.Category == typeof(ImageProcessingMiddleware).FullName);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("PathPattern", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/media", entry.Message, StringComparison.Ordinal);
    }

    private async Task<Host> StartAsync(string? pathPattern)
    {
        var logs = new CapturingLoggerProvider();
        IHost host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureLogging(logging => logging.AddProvider(logs))
                .ConfigureServices(services =>
                {
                    services.AddImageProcessing(options =>
                    {
                        options.Sources = [new ImageSourceOptions { RoutePrefix = "/media", PathPattern = pathPattern }];
                        options.OriginalsRootPath = Path.Combine(_root, "originals");
                        options.DerivativeCacheRootPath = null;
                    });
                    services.AddSingleton<IImageProcessor, UnreachableImageProcessor>();
                })
                .Configure(app =>
                {
                    app.UseImageProcessing();
                    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(_root, "wwwroot")) });
                }))
            .StartAsync();

        return new Host(host, logs);
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private sealed class Host(IHost host, CapturingLoggerProvider logs) : IAsyncDisposable
    {
        public HttpClient Client { get; } = host.GetTestClient();

        public CapturingLoggerProvider Logs { get; } = logs;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.StopAsync();
            host.Dispose();
        }
    }

    /// <summary>No test here sends a processing command, so the processor must never be asked for anything.</summary>
    private sealed class UnreachableImageProcessor : IImageProcessor
    {
        public string Name => "Unreachable";

        public IReadOnlyCollection<string> SupportedOutputFormats => [];

        public Task ProcessAsync(Stream source, Stream destination, ResolvedImageCommand command, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The processor must not be reached by these tests.");
    }
}
