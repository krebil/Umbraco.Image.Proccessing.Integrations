using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;

namespace Umbraco.Image.Processing.IntegrationTests.Shared;

/// <summary>
/// Builds HMAC-signed (and deliberately mis-signed) request URLs against an
/// <see cref="Umbraco.Image.Processing.Core.Middleware.ImageProcessingMiddleware" />-mounted host, for
/// the accept/tampered/unsigned scenarios both the Service and the in-process Umbraco sample's pipeline
/// tests need (production-hardening ticket 07) — pulled out here so neither host's test project
/// reimplements <see cref="HmacSigner" />'s canonicalization by hand.
/// </summary>
public sealed class SignedRequestUrlBuilder
{
    private readonly HmacSigner _signer;

    public SignedRequestUrlBuilder(byte[] hmacSecretKey) =>
        _signer = new HmacSigner(Options.Create(new ImageProcessingOptions { HmacSecretKey = hmacSecretKey }));

    /// <summary>A correctly signed request URL — the "accept" case.</summary>
    public string Signed(string path, params (string Key, string Value)[] query)
    {
        Dictionary<string, string?> queryDictionary = ToDictionary(query);
        string? token = _signer.ComputeToken(new PathString(path), ToQueryCollection(queryDictionary));
        if (token is not null)
        {
            queryDictionary[ImageProcessingCommandNames.HmacToken] = token;
        }

        return QueryHelpers.AddQueryString(path, queryDictionary);
    }

    /// <summary>The same request with no <c>hmac</c> token at all — the "unsigned" reject case.</summary>
    public string Unsigned(string path, params (string Key, string Value)[] query) =>
        QueryHelpers.AddQueryString(path, ToDictionary(query));

    /// <summary>A syntactically present but wrong <c>hmac</c> token — the "tampered" reject case.</summary>
    public string Tampered(string path, params (string Key, string Value)[] query)
    {
        Dictionary<string, string?> queryDictionary = ToDictionary(query);
        queryDictionary[ImageProcessingCommandNames.HmacToken] = new string('0', 64);
        return QueryHelpers.AddQueryString(path, queryDictionary);
    }

    private static Dictionary<string, string?> ToDictionary((string Key, string Value)[] query)
    {
        var dictionary = new Dictionary<string, string?>();
        foreach ((string key, string value) in query)
        {
            dictionary[key] = value;
        }

        return dictionary;
    }

    private static QueryCollection ToQueryCollection(Dictionary<string, string?> query) =>
        new(query.ToDictionary(kvp => kvp.Key, kvp => new StringValues(kvp.Value)));
}
