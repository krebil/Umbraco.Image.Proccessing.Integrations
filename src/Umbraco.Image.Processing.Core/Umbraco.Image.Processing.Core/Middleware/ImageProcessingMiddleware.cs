using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Processing;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.Middleware;

/// <summary>
/// Parses, authorizes, and serves image requests under the configured
/// <see cref="ImageProcessingOptions.Sources" />: mountable both in-process (into the existing Umbraco
/// pipeline) and standalone (into a bare ASP.NET Core app). Each request is served from the original-image
/// storage of the source <see cref="ImageSourceMatcher" /> picks for its path.
/// </summary>
public sealed class ImageProcessingMiddleware(
    RequestDelegate next,
    IOptions<ImageProcessingOptions> options,
    ImageSourceMatcher sourceMatcher,
    IServiceProvider serviceProvider,
    IDerivativeImageCache derivativeImageCache,
    IHmacSigner hmacSigner,
    IImageProcessor processor,
    ILogger<ImageProcessingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ImageProcessingOptions _options = options.Value;
    private readonly ImageSourceMatcher _sourceMatcher = sourceMatcher;
    private readonly Dictionary<string, IOriginalImageSource> _originalImageSources = sourceMatcher.RoutePrefixes.ToDictionary(
        prefix => prefix,
        prefix => serviceProvider.GetKeyedService<IOriginalImageSource>(prefix) ?? serviceProvider.GetRequiredService<IOriginalImageSource>());
    private readonly IDerivativeImageCache _derivativeImageCache = derivativeImageCache;
    private readonly IHmacSigner _hmacSigner = hmacSigner;
    private readonly IImageProcessor _processor = processor;
    private readonly ILogger<ImageProcessingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_sourceMatcher.TryMatch(context.Request.Path, out ImageSourceMatch? match))
        {
            await _next(context);
            return;
        }

        string relativePath = match.RelativePath;

        ParsedImageCommand parsed = ImageCommandParser.Parse(context.Request.Query, _options);

        // HMAC only guards against tampering with processing commands (width/height/crop/etc.) — a
        // bare passthrough request has no commands to tamper with, so it's exempt even when signing
        // is enabled. This also covers media URLs that never went through the URL generator at all
        // (raw links, rich-text-embedded media), which never carry a token in the first place.
        if (parsed.HasProcessingCommands &&
            !_hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
        {
            _logger.LogWarning("Rejected image request {Path} — HMAC token missing or invalid.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Stream? source = await _originalImageSources[match.RoutePrefix].OpenReadAsync(relativePath, context.RequestAborted);
        if (source is null)
        {
            await _next(context);
            return;
        }

        await using (source)
        {
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

            string cacheKey = $"{match.RoutePrefix}{relativePath}?{context.Request.QueryString}";
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
