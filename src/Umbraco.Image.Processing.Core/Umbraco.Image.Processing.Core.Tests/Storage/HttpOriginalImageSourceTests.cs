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

    private readonly byte[] _hmacSecretKey = RandomNumberGenerator.GetBytes(32);
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

                    if (context.Request.Path == $"{HttpOriginalImageSource.OriginRoutePrefix}{RelativePath}")
                    {
                        await context.Response.Body.WriteAsync(KnownBytes);
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
    public async Task OpenReadAsync_MissingPath_ReturnsNull()
    {
        HttpOriginalImageSource source = CreateSource(_hmacSecretKey);

        Stream? stream = await source.OpenReadAsync("/does-not-exist.jpg");

        Assert.Null(stream);
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

    private HttpOriginalImageSource CreateSource(byte[] hmacSecretKey)
    {
        HttpClient client = _originHost.GetTestClient();
        var signerOptions = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = hmacSecretKey });
        var signer = new HmacSigner(signerOptions);
        var sourceOptions = MicrosoftOptions.Create(new HttpOriginalImageSourceOptions { UmbracoBaseUrl = client.BaseAddress!.ToString().TrimEnd('/') });
        return new HttpOriginalImageSource(client, signer, sourceOptions);
    }
}
