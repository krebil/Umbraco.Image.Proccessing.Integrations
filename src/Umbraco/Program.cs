using ImageProcessingDemo;
using Umbraco.Cms.Core.DependencyInjection;
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
var storageMode = imageProcessingSection.GetValue("Storage:Mode", ImageStorageMode.LocalDisk);

IUmbracoBuilder umbracoBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers();

if (storageMode == ImageStorageMode.AzureBlob)
{
    // Umbraco.StorageProviders.AzureBlob auto-binds AzureBlobFileSystemOptions from
    // Umbraco:Storage:AzureBlob:Media (its own native config path — a different section from this
    // repo's own ImageProcessing:Storage:AzureBlob, which the standalone Service's
    // AzureBlobOriginalImageSource reads separately). The two must point at the same container so
    // both sides read/write the same media (production-hardening ticket 11).
    umbracoBuilder.AddAzureBlobMediaFileSystem();
}

umbracoBuilder.Build();

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

// Test-support only — see E2ETestSupportEndpoints' own doc comment (production-hardening ticket 11).
app.MapE2ETestSupportEndpoints();

// Real production endpoint, not test-only — see OriginImageEndpoints' own doc comment
// (production-hardening ticket 12).
app.MapOriginImageEndpoints();

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

/// <summary>
/// Where this sample's media is physically stored. AzureBlob wires Umbraco's own media file system
/// to Blob Storage (<c>Umbraco.StorageProviders.AzureBlob</c>) instead of local disk — orthogonal to
/// <see cref="ImageProcessingMode" />, which only governs how image *requests* are handled.
/// </summary>
internal enum ImageStorageMode
{
    LocalDisk,
    AzureBlob,
}

// The compiler-generated top-level-statements Program class is already public in ASP.NET Core Web
// SDK projects, so integration tests can boot this app via WebApplicationFactory<Program> without an
// explicit partial class declaration here (ASP0027).
