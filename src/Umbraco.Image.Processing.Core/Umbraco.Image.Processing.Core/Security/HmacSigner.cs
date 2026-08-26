using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Options;

namespace Umbraco.Image.Processing.Core.Security;

/// <summary>
/// HMAC-SHA256 over the path plus the sorted, non-<c>hmac</c> query commands, hex-encoded. Signing and
/// verification both build the same canonical string, so the token added by
/// <c>ImageProcessingUrlGenerator</c> validates against whatever the request's actual command set is.
/// </summary>
public sealed class HmacSigner(IOptions<ImageProcessingOptions> options) : IHmacSigner
{
    private readonly ImageProcessingOptions _options = options.Value;

    public bool IsEnabled => _options.HmacSecretKey is { Length: > 0 };

    public string? ComputeToken(PathString path, IQueryCollection query)
    {
        if (!IsEnabled)
        {
            return null;
        }

        string canonical = BuildCanonicalString(path, query);
        byte[] hash = HMACSHA256.HashData(_options.HmacSecretKey!, Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    public bool Validate(PathString path, IQueryCollection query, string? token)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string? expected = ComputeToken(path, query);
        if (expected is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(token.ToLowerInvariant()));
    }

    private static string BuildCanonicalString(PathString path, IQueryCollection query)
    {
        IEnumerable<string> pairs = query
            .Where(kvp => !string.Equals(kvp.Key, ImageProcessingCommandNames.HmacToken, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");

        return $"{path}?{string.Join('&', pairs)}";
    }
}
