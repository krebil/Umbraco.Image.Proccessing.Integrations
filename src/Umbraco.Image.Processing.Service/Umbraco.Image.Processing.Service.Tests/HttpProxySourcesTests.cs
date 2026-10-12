using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.IntegrationTests.Shared;
using Xunit;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// Two HttpProxy sources on one service instance. Each must fetch its own original from the origin
/// endpoint: the origin receives the source's full path, prefix included, and resolves the keyed
/// storage for that prefix the way the sample's <c>OriginImageEndpoints</c> does. The origin stand-in
/// listens on a real port, because the service's <see cref="HttpOriginalImageSource" /> uses a real
/// <see cref="HttpClient" />.
/// </summary>
public sealed class HttpProxySourcesTests : IAsyncDisposable
{
    private readonly byte[] _hmacSecretKey = RandomNumberGenerator.GetBytes(32);
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "service-httpproxy-cache-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, byte[]> _mediaOriginals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _archiveOriginals = new(StringComparer.Ordinal);
    private readonly List<string> _originPaths = [];
    private WebApplication? _origin;

    public async ValueTask DisposeAsync()
    {
        if (_origin is not null)
        {
            await _origin.DisposeAsync();
        }

        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EachHttpProxySource_FetchesItsOwnOriginal()
    {
        // Same relative path under both prefixes, different original images.
        _mediaOriginals["/photo.png"] = TestImages.SolidColorPngBytes(40, 20);
        _archiveOriginals["/photo.png"] = TestImages.SolidColorPngBytes(20, 40);
        string originBaseUrl = await StartOriginAsync();

        await using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:Sources:0:RoutePrefix"] = "/media",
            ["ImageProcessing:Sources:0:Storage:Mode"] = "HttpProxy",
            ["ImageProcessing:Sources:0:Storage:HttpProxy:UmbracoBaseUrl"] = originBaseUrl,
            ["ImageProcessing:Sources:1:RoutePrefix"] = "/-/archive",
            ["ImageProcessing:Sources:1:Storage:Mode"] = "HttpProxy",
            ["ImageProcessing:Sources:1:Storage:HttpProxy:UmbracoBaseUrl"] = originBaseUrl,
            ["ImageProcessing:DerivativeCacheRootPath"] = _cacheRoot,
            ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(_hmacSecretKey),
        });
        using HttpClient client = factory.CreateClient();
        var urls = new SignedRequestUrlBuilder(_hmacSecretKey);

        using SKBitmap media = await GetImageAsync(client, urls.Signed("/media/photo.png", ("width", "10")));
        using SKBitmap archive = await GetImageAsync(client, urls.Signed("/-/archive/photo.png", ("width", "10")));

        Assert.Equal((10, 5), (media.Width, media.Height));
        Assert.Equal((10, 20), (archive.Width, archive.Height));
        Assert.Equal([$"{HttpOriginalImageSource.OriginRoutePrefix}/media/photo.png", $"{HttpOriginalImageSource.OriginRoutePrefix}/-/archive/photo.png"], _originPaths);
    }

    /// <summary>
    /// Mirrors the sample's <c>OriginImageEndpoints</c>: validates the HMAC, matches the path after the
    /// origin prefix with the same source rules the service uses, and reads the keyed storage for that source.
    /// </summary>
    private async Task<string> StartOriginAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(Options.Create(new ImageProcessingOptions
        {
            HmacSecretKey = _hmacSecretKey,
            Sources = [new ImageSourceOptions { RoutePrefix = "/media" }, new ImageSourceOptions { RoutePrefix = "/-/archive" }],
        }));
        builder.Services.AddSingleton<IHmacSigner, HmacSigner>();
        builder.Services.AddSingleton<ImageSourceMatcher>();
        builder.Services.AddKeyedSingleton<IOriginalImageSource>("/media", new DictionaryImageSource(_mediaOriginals));
        builder.Services.AddKeyedSingleton<IOriginalImageSource>("/-/archive", new DictionaryImageSource(_archiveOriginals));

        _origin = builder.Build();
        _origin.MapGet($"{HttpOriginalImageSource.OriginRoutePrefix}/{{**path}}", async (HttpContext context, IHmacSigner hmacSigner, ImageSourceMatcher matcher) =>
        {
            _originPaths.Add(context.Request.Path.Value!);
            if (!hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            var sourcePath = new PathString("/" + ((string?)context.GetRouteValue("path") ?? string.Empty));
            if (!matcher.TryMatch(sourcePath, out ImageSourceMatch? match))
            {
                return Results.NotFound();
            }

            IOriginalImageSource source = context.RequestServices.GetRequiredKeyedService<IOriginalImageSource>(match.RoutePrefix);
            Stream? stream = await source.OpenReadAsync(match.RelativePath, context.RequestAborted);
            return stream is null ? Results.NotFound() : Results.Stream(stream, "application/octet-stream");
        });

        await _origin.StartAsync();
        return _origin.Urls.Single();
    }

    private static async Task<SKBitmap> GetImageAsync(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
    }

    private sealed class DictionaryImageSource(IReadOnlyDictionary<string, byte[]> originals) : IOriginalImageSource
    {
        public Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(originals.TryGetValue(requestPath, out byte[]? bytes) ? new MemoryStream(bytes, writable: false) : null);
    }
}
