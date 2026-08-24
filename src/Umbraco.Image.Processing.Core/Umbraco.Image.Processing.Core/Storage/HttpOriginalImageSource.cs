using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;

namespace Umbraco.Image.Processing.Core.Storage;

/// <summary>
/// Reads originals over HTTP from Umbraco's own raw-original endpoint — for the deployment shape
/// neither <c>LocalDiskOriginalImageSource</c> nor <c>AzureBlobOriginalImageSource</c> covers: Umbraco
/// and the standalone Service are genuinely separate deployments with no shared disk/volume, and
/// Umbraco's media isn't Blob-backed either. The Service has to ask Umbraco itself for the bytes.
/// </summary>
/// <remarks>
/// Requests go to <see cref="OriginRoutePrefix" />, not <c>ImageProcessingOptions.RoutePrefix</c>
/// (typically <c>/media</c>): Umbraco's Standalone-mode redirect middleware only matches requests that
/// start with <c>RoutePrefix</c>, so a request under this separate, unrelated prefix never enters that
/// matching logic and can't be bounced straight back to the Service — no bypass branch needed in the
/// redirect middleware itself, no risk of an exemption there accidentally also matching real browser
/// traffic. The request is signed with the same <see cref="IHmacSigner" /> Umbraco's endpoint validates
/// inbound requests with — reusing it directly rather than inventing a second secret/scheme — so the
/// route isn't an unauthenticated bulk-download of every original; the route change and the signature
/// are independent layers, one avoids the loop, the other stops unauthenticated access.
/// </remarks>
public sealed class HttpOriginalImageSource : IOriginalImageSource
{
    /// <summary>
    /// Path prefix Umbraco mounts its raw-original endpoint at. Fixed, not configurable: both sides
    /// (this class, building requests, and Umbraco's own endpoint mapping, matching them) have to agree
    /// on it, and there's no scenario where either side would need to move it independently of the other.
    /// </summary>
    public const string OriginRoutePrefix = "/__image-origin";

    private readonly HttpClient _httpClient;
    private readonly IHmacSigner _hmacSigner;
    private readonly string _umbracoBaseUrl;

    public HttpOriginalImageSource(HttpClient httpClient, IHmacSigner hmacSigner, IOptions<HttpOriginalImageSourceOptions> options)
    {
        _httpClient = httpClient;
        _hmacSigner = hmacSigner;
        _umbracoBaseUrl = options.Value.UmbracoBaseUrl.TrimEnd('/');
    }

    public async Task<Stream?> OpenReadAsync(string requestPath, CancellationToken cancellationToken = default)
    {
        var originPath = new PathString($"{OriginRoutePrefix}/{requestPath.TrimStart('/', '\\')}");
        string? token = _hmacSigner.ComputeToken(originPath, QueryCollection.Empty);
        string url = token is null
            ? $"{_umbracoBaseUrl}{originPath}"
            : $"{_umbracoBaseUrl}{originPath}?{ImageProcessingCommandNames.HmacToken}={token}";

        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Covers both "genuinely missing" (404 from Umbraco's own IOriginalImageSource returning
            // null) and "rejected" (400 from a missing/invalid HMAC token) — the middleware treats both
            // as "fall through to the next handler" the same way it does for the other two sources'
            // null return, so there's no reason to distinguish them here.
            return null;
        }

        // Buffered, not streamed straight off the response: same reason as AzureBlobOriginalImageSource
        // — the interface contract requires a seekable stream (the middleware reads image headers, then
        // rewinds to reprocess), and HttpContent's stream is forward-only.
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new MemoryStream(bytes, writable: false);
    }
}
