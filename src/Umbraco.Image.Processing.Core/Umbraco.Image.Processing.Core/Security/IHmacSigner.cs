using Microsoft.AspNetCore.Http;

namespace Umbraco.Image.Processing.Core.Security;

/// <summary>
/// Signs and verifies image request URLs, shared by the URL generator (signing) and the middleware
/// (verification) so both agree on the same canonical form.
/// </summary>
public interface IHmacSigner
{
    /// <summary>
    /// <see langword="false" /> when no secret key is configured — signing/verification is a no-op.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Computes the token for <paramref name="path" />/<paramref name="query" />, or <see langword="null" />
    /// when <see cref="IsEnabled" /> is <see langword="false" />.
    /// </summary>
    string? ComputeToken(PathString path, IQueryCollection query);

    /// <summary>
    /// Validates <paramref name="token" /> against <paramref name="path" />/<paramref name="query" />.
    /// Always <see langword="true" /> when <see cref="IsEnabled" /> is <see langword="false" />.
    /// </summary>
    bool Validate(PathString path, IQueryCollection query, string? token);
}
