using System.Text;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.UmbracoExtensions.Tests.UrlGeneration;

public class ImageProcessingUrlGeneratorTests
{
    private static ImageProcessingUrlGenerator CreateGenerator(byte[]? hmacSecret = null, string? externalBaseUrl = null)
    {
        var options = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = hmacSecret, ExternalBaseUrl = externalBaseUrl });
        return new ImageProcessingUrlGenerator(options, new HmacSigner(options));
    }

    [Fact]
    public void ReturnsNullWhenImageUrlIsNull()
    {
        Assert.Null(CreateGenerator().GetImageUrl(new ImageUrlGenerationOptions(null)));
    }

    [Fact]
    public void BuildsWidthHeightFormatQualityQueryString()
    {
        var options = new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300, Height = 200, Format = "webp", Quality = 80 };

        string? url = CreateGenerator().GetImageUrl(options);

        Assert.Equal("/media/foo.jpg?format=webp&height=200&quality=80&width=300", url);
    }

    [Fact]
    public void BuildsCropCommand()
    {
        var options = new ImageUrlGenerationOptions("/media/foo.jpg")
        {
            Crop = new ImageUrlGenerationOptions.CropCoordinates(0.1m, 0.2m, 0.3m, 0.4m),
        };

        string? url = CreateGenerator().GetImageUrl(options);

        Assert.Equal("/media/foo.jpg?cc=0.1,0.2,0.3,0.4", url);
    }

    [Fact]
    public void AppendsValidHmacTokenWhenSigningEnabled()
    {
        byte[] secret = Encoding.UTF8.GetBytes("test-secret");
        ImageProcessingUrlGenerator generator = CreateGenerator(secret);
        var options = new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300 };

        string? url = generator.GetImageUrl(options);

        Assert.Contains("&hmac=", url);

        var signer = new HmacSigner(MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = secret }));
        Uri parsed = new(new Uri("http://localhost"), url!);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query);
        string token = query["hmac"].ToString();

        var queryWithoutToken = new Microsoft.AspNetCore.Http.QueryCollection(
            query.Where(kvp => kvp.Key != "hmac").ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

        Assert.True(signer.Validate(parsed.AbsolutePath, queryWithoutToken, token));
    }

    [Fact]
    public void AppendsValidHmacTokenForMultipleCommands()
    {
        byte[] secret = Encoding.UTF8.GetBytes("test-secret");
        ImageProcessingUrlGenerator generator = CreateGenerator(secret);
        var options = new ImageUrlGenerationOptions("/media/foo.jpg")
        {
            Width = 300,
            Height = 300,
            Crop = new ImageUrlGenerationOptions.CropCoordinates(0.25m, 0.25m, 0.25m, 0.25m),
        };

        string? url = generator.GetImageUrl(options);

        var signer = new HmacSigner(MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = secret }));
        Uri parsed = new(new Uri("http://localhost"), url!);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query);
        string token = query["hmac"].ToString();

        var queryWithoutToken = new Microsoft.AspNetCore.Http.QueryCollection(
            query.Where(kvp => kvp.Key != "hmac").ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

        Assert.True(signer.Validate(parsed.AbsolutePath, queryWithoutToken, token));
    }

    [Fact]
    public void AppendsValidHmacTokenWhenNoOtherCommandsPresent()
    {
        byte[] secret = Encoding.UTF8.GetBytes("test-secret");
        ImageProcessingUrlGenerator generator = CreateGenerator(secret);
        var options = new ImageUrlGenerationOptions("/media/foo.jpg");

        string? url = generator.GetImageUrl(options);

        Assert.Contains("hmac=", url);
    }

    [Fact]
    public void OmitsHmacTokenWhenSigningDisabled()
    {
        string? url = CreateGenerator().GetImageUrl(new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300 });

        Assert.DoesNotContain("hmac", url);
    }

    [Fact]
    public void PrefixesExternalBaseUrlWhenConfigured()
    {
        ImageProcessingUrlGenerator generator = CreateGenerator(externalBaseUrl: "http://localhost:5050");
        var options = new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300 };

        string? url = generator.GetImageUrl(options);

        Assert.Equal("http://localhost:5050/media/foo.jpg?width=300", url);
    }

    [Fact]
    public void TrimsTrailingSlashFromExternalBaseUrl()
    {
        ImageProcessingUrlGenerator generator = CreateGenerator(externalBaseUrl: "http://localhost:5050/");
        var options = new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300 };

        string? url = generator.GetImageUrl(options);

        Assert.Equal("http://localhost:5050/media/foo.jpg?width=300", url);
    }

    [Fact]
    public void DoesNotPrefixUrlWhenExternalBaseUrlNotConfigured()
    {
        string? url = CreateGenerator().GetImageUrl(new ImageUrlGenerationOptions("/media/foo.jpg") { Width = 300 });

        Assert.Equal("/media/foo.jpg?width=300", url);
    }
}
