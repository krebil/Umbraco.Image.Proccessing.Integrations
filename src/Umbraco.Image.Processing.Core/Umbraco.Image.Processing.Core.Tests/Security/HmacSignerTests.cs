using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.Core.Tests.Security;

public class HmacSignerTests
{
    private static HmacSigner CreateSigner(byte[]? secret) =>
        new(MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = secret }));

    private static IQueryCollection Query(params (string Key, string Value)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value)));

    [Fact]
    public void DisabledWhenNoSecretConfigured()
    {
        HmacSigner signer = CreateSigner(null);

        Assert.False(signer.IsEnabled);
        Assert.Null(signer.ComputeToken("/media/foo.jpg", Query(("width", "300"))));
        Assert.True(signer.Validate("/media/foo.jpg", Query(("width", "300")), token: null));
    }

    [Fact]
    public void ComputedTokenValidatesSuccessfully()
    {
        HmacSigner signer = CreateSigner(Encoding.UTF8.GetBytes("test-secret"));
        var query = Query(("width", "300"), ("height", "200"));

        string? token = signer.ComputeToken("/media/foo.jpg", query);

        Assert.False(string.IsNullOrEmpty(token));
        Assert.True(signer.Validate("/media/foo.jpg", query, token));
    }

    [Fact]
    public void TokenIsIndependentOfQueryParameterOrder()
    {
        HmacSigner signer = CreateSigner(Encoding.UTF8.GetBytes("test-secret"));

        string? token1 = signer.ComputeToken("/media/foo.jpg", Query(("width", "300"), ("height", "200")));
        string? token2 = signer.ComputeToken("/media/foo.jpg", Query(("height", "200"), ("width", "300")));

        Assert.Equal(token1, token2);
    }

    [Fact]
    public void RejectsTamperedQuery()
    {
        HmacSigner signer = CreateSigner(Encoding.UTF8.GetBytes("test-secret"));
        string? token = signer.ComputeToken("/media/foo.jpg", Query(("width", "300")));

        bool valid = signer.Validate("/media/foo.jpg", Query(("width", "999")), token);

        Assert.False(valid);
    }

    [Fact]
    public void RejectsTamperedPath()
    {
        HmacSigner signer = CreateSigner(Encoding.UTF8.GetBytes("test-secret"));
        var query = Query(("width", "300"));
        string? token = signer.ComputeToken("/media/foo.jpg", query);

        bool valid = signer.Validate("/media/bar.jpg", query, token);

        Assert.False(valid);
    }

    [Fact]
    public void ExcludesTheHmacParameterItselfFromTheSignedValue()
    {
        HmacSigner signer = CreateSigner(Encoding.UTF8.GetBytes("test-secret"));
        var withoutToken = Query(("width", "300"));

        string? token = signer.ComputeToken("/media/foo.jpg", withoutToken);
        var withToken = Query(("width", "300"), ("hmac", token!));

        Assert.True(signer.Validate("/media/foo.jpg", withToken, token));
    }
}
