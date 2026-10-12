using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.UmbracoExtensions.DependencyInjection;
using Umbraco.Image.Processing.UmbracoExtensions.UrlGeneration;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.UmbracoExtensions.Tests.UrlGeneration;

public class ImageProcessingUrlGeneratorTests
{
    private static ImageProcessingUrlGenerator CreateGenerator(byte[]? hmacSecret = null, string? externalBaseUrl = null)
    {
        var options = MicrosoftOptions.Create(new ImageProcessingOptions { HmacSecretKey = hmacSecret, ExternalBaseUrl = externalBaseUrl });
        return new ImageProcessingUrlGenerator(options, new HmacSigner(options), new ImageSourceMatcher(options));
    }

    [Fact]
    public void ResolvesFromTheContainer_WithTheRegisteredSources()
    {
        // The generator takes the one ImageSourceMatcher AddImageProcessing() registers, so the URLs it
        // makes absolute are exactly the paths the middleware and any redirect serve.
        var services = new ServiceCollection();
        services.AddImageProcessing(options =>
        {
            options.ExternalBaseUrl = "http://images.example";
            options.Sources = [new ImageSourceOptions { RoutePrefix = "/media" }, new ImageSourceOptions { RoutePrefix = "/-/archive" }];
        }).AddUmbracoImageProcessing();
        using ServiceProvider provider = services.BuildServiceProvider();

        IImageUrlGenerator generator = provider.GetRequiredService<IImageUrlGenerator>();

        Assert.IsType<ImageProcessingUrlGenerator>(generator);
        Assert.Equal("http://images.example/-/archive/foo.jpg?width=300", generator.GetImageUrl(new ImageUrlGenerationOptions("/-/archive/foo.jpg") { Width = 300 }));
        Assert.Equal("/uploads/foo.jpg?width=300", generator.GetImageUrl(new ImageUrlGenerationOptions("/uploads/foo.jpg") { Width = 300 }));
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

    [Theory]
    [InlineData("/media/foo.jpg", "http://images.example/media/foo.jpg?width=300")]
    [InlineData("/-/archive/0123456789abcdef0123456789abcdef/assets/0123456789abcdef0123456789abcdef.png", "http://images.example/-/archive/0123456789abcdef0123456789abcdef/assets/0123456789abcdef0123456789abcdef.png?width=300")]
    [InlineData("/-/archive/0123456789abcdef0123456789abcdef/pages/foo.png", "/-/archive/0123456789abcdef0123456789abcdef/pages/foo.png?width=300")]
    [InlineData("/uploads/foo.jpg", "/uploads/foo.jpg?width=300")]
    public void PrefixesExternalBaseUrlOnlyForPathsAConfiguredSourceServes(string imageUrl, string expected)
    {
        var options = MicrosoftOptions.Create(new ImageProcessingOptions
        {
            ExternalBaseUrl = "http://images.example",
            Sources =
            [
                new ImageSourceOptions { RoutePrefix = "/media" },
                new ImageSourceOptions { RoutePrefix = "/-/archive", PathPattern = @"^[0-9a-f]{32}/assets/[0-9a-f]{32}\.[a-z0-9]{1,10}$" },
            ],
        });
        var generator = new ImageProcessingUrlGenerator(options, new HmacSigner(options), new ImageSourceMatcher(options));

        string? url = generator.GetImageUrl(new ImageUrlGenerationOptions(imageUrl) { Width = 300 });

        Assert.Equal(expected, url);
    }
}
