using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Processing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.Middleware;

/// <summary>
/// Parses, authorizes, and serves image requests: mountable both in-process (into the existing
/// Umbraco pipeline, under <see cref="ImageProcessingOptions.RoutePrefix" />) and standalone (into a
/// bare ASP.NET Core app, with an empty <see cref="ImageProcessingOptions.RoutePrefix" />).
/// </summary>
public sealed class ImageProcessingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ImageProcessingOptions _options;
    private readonly IOriginalImageSource _originalImageSource;
    private readonly IDerivativeImageCache _derivativeImageCache;
    private readonly IHmacSigner _hmacSigner;
    private readonly IImageProcessor _processor;
    private readonly ILogger<ImageProcessingMiddleware> _logger;

    public ImageProcessingMiddleware(
        RequestDelegate next,
        IOptions<ImageProcessingOptions> options,
        IOriginalImageSource originalImageSource,
        IDerivativeImageCache derivativeImageCache,
        IHmacSigner hmacSigner,
        IImageProcessor processor,
        ILogger<ImageProcessingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _originalImageSource = originalImageSource;
        _derivativeImageCache = derivativeImageCache;
        _hmacSigner = hmacSigner;
        _processor = processor;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsImageRequest(context.Request.Path, out string relativePath))
        {
            await _next(context);
            return;
        }

        if (!_hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
        {
            _logger.LogWarning("Rejected image request {Path} — HMAC token missing or invalid.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Stream? source = await _originalImageSource.OpenReadAsync(relativePath, context.RequestAborted);
        if (source is null)
        {
            await _next(context);
            return;
        }

        await using (source)
        {
            ParsedImageCommand parsed = ImageCommandParser.Parse(context.Request.Query, _options);

            if (!parsed.HasProcessingCommands)
            {
                await WriteResponseAsync(context, source, ContentTypeFor(Path.GetExtension(relativePath).TrimStart('.')));
                return;
            }

            if (!ImageHeaderReader.TryRead(source, out ImageHeaderInfo header))
            {
                _logger.LogWarning("Could not read image header for {Path} — unsupported or corrupt file.", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                return;
            }

            source.Position = 0;

            ResolvedImageCommand resolved = ImageCommandResolver.Resolve(parsed, _options, header);

            string cacheKey = $"{relativePath}?{context.Request.QueryString}";
            Stream? cached = await _derivativeImageCache.TryOpenReadAsync(cacheKey, context.RequestAborted);
            if (cached is not null)
            {
                await using (cached)
                {
                    await WriteResponseAsync(context, cached, ContentTypeFor(resolved.Format));
                }

                return;
            }

            using var destination = new MemoryStream();
            await _processor.ProcessAsync(source, destination, resolved, context.RequestAborted);

            destination.Position = 0;
            await _derivativeImageCache.WriteAsync(cacheKey, destination, context.RequestAborted);

            destination.Position = 0;
            await WriteResponseAsync(context, destination, ContentTypeFor(resolved.Format));
        }
    }

    private bool IsImageRequest(PathString path, out string relativePath)
    {
        relativePath = string.Empty;

        PathString remaining;
        if (string.IsNullOrEmpty(_options.RoutePrefix))
        {
            // Standalone mode: the whole app is the image service, so every path is a candidate.
            remaining = path;
        }
        else if (!path.StartsWithSegments(_options.RoutePrefix, out remaining))
        {
            return false;
        }

        if (!_options.SupportedRequestExtensions.Contains(Path.GetExtension(remaining.Value ?? string.Empty)))
        {
            return false;
        }

        relativePath = remaining.Value ?? string.Empty;
        return true;
    }

    private async Task WriteResponseAsync(HttpContext context, Stream content, string contentType)
    {
        context.Response.ContentType = contentType;
        context.Response.Headers.CacheControl = $"public, max-age={(int)_options.BrowserCacheMaxAge.TotalSeconds}";
        content.Position = 0;
        await content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static string ContentTypeFor(string format) => format.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "gif" => "image/gif",
        "bmp" => "image/bmp",
        "webp" => "image/webp",
        _ => "application/octet-stream",
    };
}
