using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;

namespace Umbraco.Image.Processing.E2E.Tests;

/// <summary>
/// Boots the real product graph (<c>umbraco</c> + <c>image-processing-service</c> + Azurite) via the
/// real <c>Umbraco.Image.Processing.AppHost</c>, as genuinely separate processes communicating over
/// real HTTP — not <c>WebApplicationFactory</c>/TestServer in-process simulation. This is what makes
/// production-hardening ticket 11's suite an actual end-to-end test of the redirect/URL-generation
/// wiring described in ADR-0006 and the standalone deployment plan, not just a proof that two
/// configuration values happen to agree.
/// </summary>
/// <remarks>
/// Must match <see cref="Umbraco.Image.Processing.Core.Options.ImageProcessingOptions.HmacSecretKey" />
/// as configured in <c>src/Umbraco/appsettings.json</c> — not a real secret, just this reference
/// implementation's fixed dev value, reused here (as <see cref="MediaResolutionTests" /> in
/// <c>Service.Tests</c> reuses its own fresh key) so requests this harness signs are accepted by the
/// real running app's HMAC validation.
/// </remarks>
public sealed class UmbracoServiceGraphHarness : IAsyncDisposable
{
    private const string HmacSecretKeyBase64 = "oquuH0J8Eqf3wT+aQnWtfE8O2/cuSAhlbfHkYLpb5m48Zi+3HJUaYpS6IlcJ8tBh9e1cWopDAZsTMUy1mNsNPQ==";
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromMinutes(3);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "e2e-umbraco-db-" + Guid.NewGuid().ToString("N"), "Umbraco.sqlite.db");
    private DistributedApplication? _app;

    public HttpClient UmbracoClient { get; private set; } = null!;

    public HttpClient ServiceClient { get; private set; } = null!;

    /// <summary>
    /// Starts the whole graph. <paramref name="storageMode" /> is <c>"LocalDisk"</c> (default),
    /// <c>"AzureBlob"</c>, or <c>"HttpProxy"</c> — forwarded straight to <c>AppHost.cs</c>'s own
    /// <c>Storage:Mode</c> switch. Always runs Umbraco in Standalone image-processing mode: LocalDisk
    /// mode's redirect-to-Service behavior, Blob mode, and HttpProxy mode's reverse fetch all only
    /// matter when Umbraco isn't handling image requests itself.
    /// </summary>
    public async Task StartAsync(string storageMode = "LocalDisk", CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        string dbConnectionString = $"Data Source={_dbPath};Cache=Shared;Foreign Keys=True;Pooling=True";

        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Umbraco_Image_Processing_AppHost>(
        [
            "--ImageProcessing:Mode=Standalone",
            $"--Storage:Mode={storageMode}",
            $"--ConnectionStrings:umbracoDbDSN={dbConnectionString}",
        ], cancellationToken);

        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        using var readinessCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readinessCts.CancelAfter(ReadinessTimeout);

        await _app.ResourceNotifications.WaitForResourceAsync("umbraco", KnownResourceStates.Running, readinessCts.Token);
        await _app.ResourceNotifications.WaitForResourceAsync("image-processing-service", KnownResourceStates.Running, readinessCts.Token);

        UmbracoClient = _app.CreateHttpClient("umbraco", "http");
        ServiceClient = _app.CreateHttpClient("image-processing-service", "http");

        // "Running" only means the process started — Umbraco's own unattended install/migrations still
        // need to finish before Kestrel starts accepting connections (Program.cs awaits
        // BootUmbracoAsync() before RunAsync()), so poll the actual port rather than trusting the
        // coarse-grained resource state alone.
        await PollUntilRespondingAsync(UmbracoClient, "/", readinessCts.Token);
        await PollUntilRespondingAsync(ServiceClient, "/", readinessCts.Token);
    }

    /// <summary>
    /// Saves <paramref name="content" /> as real Umbraco media via <see cref="E2ETestSupportEndpoints" />
    /// (exercising Umbraco's real <c>IMediaService</c> save path, not a hand-written file), returning
    /// the URL-shaped relative path (e.g. <c>/media/1234/photo.jpg</c>) it was stored at.
    /// </summary>
    public async Task<string> SaveMediaAsync(byte[] content, string filename, CancellationToken cancellationToken = default)
    {
        using var body = new ByteArrayContent(content);
        using HttpResponseMessage response = await UmbracoClient.PostAsync($"/e2e-test-support/media?filename={Uri.EscapeDataString(filename)}", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string diagBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SaveMediaAsync failed: {(int)response.StatusCode} {response.StatusCode}\n{diagBody}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Builds an HMAC-signed resize request path for <paramref name="relativeMediaUrl" /> the same way
    /// <see cref="ImageProcessingUrlGenerator" /> — the real class Umbraco calls in-process to generate
    /// these URLs — would, without pointing it at a live app (no <c>ExternalBaseUrl</c>, no DI): the
    /// resulting relative path is issued directly against <see cref="ServiceClient" />.
    /// </summary>
    public string SignedResizeUrl(string relativeMediaUrl, int width)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ImageProcessingOptions { HmacSecretKey = Convert.FromBase64String(HmacSecretKeyBase64) });
        var generator = new ImageProcessingUrlGenerator(options, new HmacSigner(options));
        return generator.GetImageUrl(new ImageUrlGenerationOptions(relativeMediaUrl) { Width = width })
            ?? throw new InvalidOperationException("ImageProcessingUrlGenerator returned no URL.");
    }

    /// <summary>
    /// Builds an HMAC-signed request path for Umbraco's raw-original endpoint
    /// (<see cref="HttpOriginalImageSource.OriginRoutePrefix" />) the same way
    /// <see cref="HttpOriginalImageSource" /> — the real class the Service uses in HttpProxy storage
    /// mode — would, without going through the Service at all: issued directly against
    /// <see cref="UmbracoClient" />, this proves the endpoint itself is reachable and correctly signed,
    /// independent of whether the Service's own round trip to it also works.
    /// </summary>
    public string SignedOriginUrl(string relativeMediaUrl)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ImageProcessingOptions { HmacSecretKey = Convert.FromBase64String(HmacSecretKeyBase64) });
        var signer = new HmacSigner(options);

        string relative = relativeMediaUrl.StartsWith(options.Value.RoutePrefix, StringComparison.OrdinalIgnoreCase)
            ? relativeMediaUrl[options.Value.RoutePrefix.Length..]
            : relativeMediaUrl;
        var path = new PathString($"{HttpOriginalImageSource.OriginRoutePrefix}{relative}");

        string? token = signer.ComputeToken(path, QueryCollection.Empty);
        return token is null ? path.Value! : $"{path}?hmac={token}";
    }

    private static async Task PollUntilRespondingAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        Exception? lastFailure = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
                return; // Any response at all means the port is up and the app pipeline is live.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastFailure = ex;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new TimeoutException($"{client.BaseAddress} never started responding within the readiness timeout.", lastFailure);
    }

    public async ValueTask DisposeAsync()
    {
        UmbracoClient?.Dispose();
        ServiceClient?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        string? dbDirectory = Path.GetDirectoryName(_dbPath);
        if (dbDirectory is not null && Directory.Exists(dbDirectory))
        {
            Directory.Delete(dbDirectory, recursive: true);
        }
    }
}
