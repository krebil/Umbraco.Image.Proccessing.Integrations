using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.Core.Tests.Storage;

/// <summary>
/// Contract tests for <see cref="HttpOriginalImageSource" />: found → stream with the expected bytes,
/// not found (or rejected) → <see langword="null" />, the same contract <c>ImageProcessingMiddleware</c>
/// already relies on for the other two <see cref="IOriginalImageSource" /> implementations. Stands up a
/// minimal in-process <see cref="TestServer" /> as a stand-in for Umbraco's own
/// <see cref="HttpOriginalImageSource.OriginRoutePrefix" /> endpoint — enough to prove this class's own
/// request-building and response-handling without Docker/Aspire. Per ADR-0008, the real cross-process
/// redirect-loop behavior this route exists to avoid is covered separately, in
/// <c>Umbraco.Image.Processing.E2E.Tests</c>, since a same-process TestServer call can't exercise the
/// real network round trip a loop would depend on.
/// </summary>
public sealed class HttpOriginalImageSourceTests : IAsyncDisposable
{
    private const string RelativePath = "/1234/photo.jpg";
    private static readonly byte[] KnownBytes = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] ArchiveBytes = [9, 9, 9];

    private readonly byte[] _hmacSecretKey = RandomNumberGenerator.GetBytes(32);
    private readonly List<string> _requestTargets = [];
    private readonly IHost _originHost;

    public HttpOriginalImageSourceTests()
    {
        // Mirrors OriginImageEndpoints.cs: validates the same HMAC token the real endpoint would, then
        // serves known bytes for the one known path, 404 for everything else.
        _originHost = new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    var options = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = _hmacSecretKey });
                    services.AddSingleton<IHmacSigner>(new HmacSigner(options));
                })
                .Configure(app => app.Run(async context =>
                {
                    var signer = context.RequestServices.GetRequiredService<IHmacSigner>();
                    if (!signer.Validate(context.Request.Path, context.Request.Query, context.Request.Query["hmac"]))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    // The origin endpoint receives the source's own path, prefix included, so the default
                    // /media source and a second source with the same relative path are told apart.
                    if (context.Request.Path == $"{HttpOriginalImageSource.OriginRoutePrefix}/media{RelativePath}")
                    {
                        await context.Response.Body.WriteAsync(KnownBytes);
                        return;
                    }

                    if (context.Request.Path == $"{HttpOriginalImageSource.OriginRoutePrefix}/-/archive{RelativePath}")
                    {
                        await context.Response.Body.WriteAsync(ArchiveBytes);
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                })))
            .Start();
    }

    public async ValueTask DisposeAsync() => await _originHost.StopAsync();

    [Fact]
    public async Task OpenReadAsync_ExistingPath_ReturnsStreamWithExpectedBytes()
    {
        HttpOriginalImageSource source = CreateSource(_hmacSecretKey);

        await using Stream? stream = await source.OpenReadAsync(RelativePath);

        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);
        Assert.Equal(KnownBytes, buffer.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_SendsTheSourcesRoutePrefix_SoTwoHttpSourcesDoNotCollide()
    {
        HttpOriginalImageSource media = CreateSource(_hmacSecretKey, routePrefix: "/Media/");
        HttpOriginalImageSource archive = CreateSource(_hmacSecretKey, routePrefix: "/-/archive");

        await using Stream? mediaStream = await media.OpenReadAsync(RelativePath);
        await using Stream? archiveStream = await archive.OpenReadAsync(RelativePath);

        Assert.Equal(KnownBytes, await ReadAsync(mediaStream));
        Assert.Equal(ArchiveBytes, await ReadAsync(archiveStream));
        Assert.Collection(
            _requestTargets,
            target => Assert.StartsWith($"{HttpOriginalImageSource.OriginRoutePrefix}/media{RelativePath}?hmac=", target, StringComparison.Ordinal),
            target => Assert.StartsWith($"{HttpOriginalImageSource.OriginRoutePrefix}/-/archive{RelativePath}?hmac=", target, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenReadAsync_MissingPath_ReturnsNull()
    {
        HttpOriginalImageSource source = CreateSource(_hmacSecretKey);

        Stream? stream = await source.OpenReadAsync("/does-not-exist.jpg");

        Assert.Null(stream);
    }

    [Theory]
    [InlineData("/..%2F..%2Fsecret.jpg")]
    [InlineData("/..%5C..%5Csecret.jpg")]
    [InlineData("/..%252F..%252Fsecret.jpg")]
    public async Task OpenReadAsync_EncodedSeparators_GoOnTheWireUnchanged(string path)
    {
        // The matcher refuses these before any storage sees them; this pins that the request itself
        // neither decodes the sequence into a real separator (which the origin's own path handling would
        // then act on) nor re-encodes it, so the origin endpoint sees exactly what was asked for and its
        // own matcher refuses it the same way.
        HttpOriginalImageSource source = CreateSource(_hmacSecretKey);

        Stream? stream = await source.OpenReadAsync(path);

        Assert.Null(stream);
        string requestTarget = Assert.Single(_requestTargets);
        Assert.Contains(path, requestTarget, StringComparison.Ordinal);
        Assert.DoesNotContain("/../", requestTarget, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenReadAsync_WrongHmacSecretKey_ReturnsNull()
    {
        // The route change and the HMAC guard are independent layers (see HttpOriginalImageSource's own
        // remarks): even hitting the right path, a source signing with the wrong key must be rejected
        // the same way a genuinely missing file is — not treated as a distinct error case.
        HttpOriginalImageSource source = CreateSource(RandomNumberGenerator.GetBytes(32));

        Stream? stream = await source.OpenReadAsync(RelativePath);

        Assert.Null(stream);
    }

    private HttpOriginalImageSource CreateSource(byte[] hmacSecretKey, string? routePrefix = null)
    {
        TestServer server = _originHost.GetTestServer();
        var client = new HttpClient(new RecordingHandler(server.CreateHandler(), _requestTargets)) { BaseAddress = server.BaseAddress };
        var signerOptions = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = hmacSecretKey });
        var signer = new HmacSigner(signerOptions);
        var options = new HttpOriginalImageSourceOptions { UmbracoBaseUrl = client.BaseAddress!.ToString().TrimEnd('/') };
        if (routePrefix is not null)
        {
            options.RoutePrefix = routePrefix;
        }

        return new HttpOriginalImageSource(client, signer, MicrosoftOptions.Create(options));
    }

    private static async Task<byte[]> ReadAsync(Stream? stream)
    {
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>Records the request target as <see cref="HttpClient" /> would put it on the wire; TestServer has no wire.</summary>
    private sealed class RecordingHandler(HttpMessageHandler inner, List<string> targets) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            targets.Add(request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
