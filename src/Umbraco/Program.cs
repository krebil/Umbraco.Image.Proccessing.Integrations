using ImageProcessingDemo;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.ImageFlow;
using Umbraco.Image.Processing.SkiaSharp;
using Umbraco.Image.Processing.UmbracoExtensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection imageProcessingSection = builder.Configuration.GetSection("ImageProcessing");
var mode = imageProcessingSection.GetValue("Mode", ImageProcessingMode.InProcess);
var processor = imageProcessingSection.GetValue<ImageProcessorKind?>("Processor")
    ?? throw new InvalidOperationException("ImageProcessing:Processor must be configured (e.g. \"SkiaSharp\" or \"ImageFlow\").");

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Registered after CreateUmbracoBuilder(), not before: Umbraco.Cms's own imaging package registers
// its own default IImageUrlGenerator/IImageDimensionExtractor, and DI's single-service resolution
// picks whichever registration is LAST regardless of Add vs TryAdd — so ours must be added after
// Umbraco's own registrations to actually be the one used. AddUmbracoImageProcessing() replaces them
// outright rather than relying on registration order alone. This app is always an Umbraco instance —
// InProcess/Standalone only changes how image *requests* are handled, not whether Umbraco's own
// IImageUrlGenerator/IImageDimensionExtractor need overriding — so the call is unconditional.
IImageProcessingBuilder imageProcessingBuilder = builder.Services.AddImageProcessing(options =>
{
    imageProcessingSection.Bind(options);
    if (mode == ImageProcessingMode.Standalone)
    {
        // Freshly-rendered pages link straight to the standalone service instead of round-tripping
        // through this app's redirect middleware below.
        options.ExternalBaseUrl ??= imageProcessingSection.GetValue<string>("Standalone:BaseUrl");
    }
}).AddUmbracoImageProcessing();

if (mode == ImageProcessingMode.InProcess)
{
    _ = processor switch
    {
        ImageProcessorKind.ImageFlow => imageProcessingBuilder.UseImageFlow(),
        _ => imageProcessingBuilder.UseSkiaSharp(),
    };
}

WebApplication app = builder.Build();

// A plain, non-Umbraco demo page so a freshly installed site has something to look at: the sample
// image at a few sizes/commands, plus a button to clear the derivative cache.
app.MapImageProcessingDemo();

if (mode == ImageProcessingMode.InProcess)
{
    // Mounted before Umbraco's own pipeline: the middleware serves media requests (resized,
    // cropped, or passed through unchanged) directly, so Umbraco's static file handling never
    // needs to see them.
    app.UseImageProcessing();
}
else
{
    var standaloneOptions = new ImageProcessingOptions();
    imageProcessingSection.Bind(standaloneOptions);
    string standaloneBaseUrl = imageProcessingSection.GetValue<string>("Standalone:BaseUrl")
        ?? throw new InvalidOperationException("ImageProcessing:Standalone:BaseUrl must be configured when ImageProcessing:Mode is Standalone.");

    // Redirects image requests to the standalone image-processing service instead of handling
    // them here — see imagesharp-standalone-service-plan.md §3.2 for the pattern this mirrors.
    app.Use(async (context, next) =>
    {
        PathString path = context.Request.Path;
        PathString remaining;
        if (string.IsNullOrEmpty(standaloneOptions.RoutePrefix))
        {
            remaining = path;
        }
        else if (!path.StartsWithSegments(standaloneOptions.RoutePrefix, out remaining))
        {
            await next();
            return;
        }

        if (!standaloneOptions.SupportedRequestExtensions.Contains(Path.GetExtension(remaining.Value ?? string.Empty)))
        {
            await next();
            return;
        }

        string target = $"{standaloneBaseUrl}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Headers.CacheControl = "public, max-age=31536000";
        context.Response.Redirect(target, permanent: false);
    });
}

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

/// <summary>
/// Where this sample's own image requests are handled: in-process, alongside Umbraco, or
/// redirected to a separately deployed standalone service.
/// </summary>
internal enum ImageProcessingMode
{
    InProcess,
    Standalone,
}

/// <summary>
/// Which <c>IImageProcessor</c> this sample registers when running in-process. Swapping this is
/// the one config-value change the whole abstraction exists to enable.
/// </summary>
internal enum ImageProcessorKind
{
    SkiaSharp,
    ImageFlow,
}
