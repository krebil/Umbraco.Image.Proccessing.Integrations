using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;

namespace Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;

/// <summary>
/// The single <see cref="IImageUrlGenerator" /> shared by every processor — output URL shape never
/// varies by which processor is active, so there's exactly one implementation. Produces Umbraco's
/// existing <c>/media/...?width=...&amp;cc=...</c> shape, HMAC-signed when configured.
/// </summary>
public sealed class ImageProcessingUrlGenerator(IOptions<ImageProcessingOptions> options, IHmacSigner hmacSigner) : IImageUrlGenerator
{
    private readonly ImageProcessingOptions _options = options.Value;
    private readonly IHmacSigner _hmacSigner = hmacSigner;

    public IEnumerable<string> SupportedImageFileTypes => _options.SupportedRequestExtensions.Select(e => e.TrimStart('.'));

    public string? GetImageUrl(ImageUrlGenerationOptions? options)
    {
        if (options?.ImageUrl is null)
        {
            return null;
        }

        var query = new SortedDictionary<string, string?>(StringComparer.Ordinal);

        if (options.Crop is { } crop)
        {
            query[ImageProcessingCommandNames.Crop] = FormattableString.Invariant($"{crop.Left},{crop.Top},{crop.Right},{crop.Bottom}");
        }

        if (options.Width is { } width)
        {
            query[ImageProcessingCommandNames.Width] = width.ToString(CultureInfo.InvariantCulture);
        }

        if (options.Height is { } height)
        {
            query[ImageProcessingCommandNames.Height] = height.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(options.Format))
        {
            query[ImageProcessingCommandNames.Format] = options.Format;
        }

        if (options.Quality is { } quality)
        {
            query[ImageProcessingCommandNames.Quality] = quality.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(options.FurtherOptions))
        {
            foreach (KeyValuePair<string, StringValues> kvp in QueryHelpers.ParseQuery(options.FurtherOptions))
            {
                query[kvp.Key] = kvp.Value.ToString();
            }
        }

        if (!string.IsNullOrEmpty(options.CacheBusterValue))
        {
            query["v"] = options.CacheBusterValue;
        }

        string url = QueryHelpers.AddQueryString(options.ImageUrl, query);

        if (_hmacSigner.IsEnabled)
        {
            var path = new PathString(GetPathOnly(options.ImageUrl));
            var queryDictionary = query.ToDictionary(kvp => kvp.Key, kvp => new StringValues(kvp.Value!));
            var queryCollection = new QueryCollection(queryDictionary);

            string? token = _hmacSigner.ComputeToken(path, queryCollection);
            if (!string.IsNullOrEmpty(token))
            {
                url = QueryHelpers.AddQueryString(url, ImageProcessingCommandNames.HmacToken, token);
            }
        }

        if (!string.IsNullOrEmpty(_options.ExternalBaseUrl))
        {
            url = _options.ExternalBaseUrl.TrimEnd('/') + url;
        }

        return url;
    }

    private static string GetPathOnly(string imageUrl) => imageUrl.Split('?', 2)[0];
}
