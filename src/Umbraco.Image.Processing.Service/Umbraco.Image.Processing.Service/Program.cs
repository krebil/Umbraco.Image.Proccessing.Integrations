using Umbraco.Image.Processing.AzureBlob.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.Core.Options;
#if INCLUDE_IMAGEFLOW
using Umbraco.Image.Processing.ImageFlow;
#endif
#if INCLUDE_SKIASHARP
using Umbraco.Image.Processing.SkiaSharp;
#endif

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection imageProcessingSection = builder.Configuration.GetSection("ImageProcessing");
var processor = imageProcessingSection.GetValue<ImageProcessorKind?>("Processor")
    ?? throw new InvalidOperationException("ImageProcessing:Processor must be configured (e.g. \"SkiaSharp\" or \"ImageFlow\").");

IImageProcessingBuilder imageProcessingBuilder = builder.Services.AddImageProcessing(options => imageProcessingSection.Bind(options));

switch (processor)
{
#if INCLUDE_SKIASHARP
    case ImageProcessorKind.SkiaSharp:
        imageProcessingBuilder.UseSkiaSharp();
        break;
#endif
#if INCLUDE_IMAGEFLOW
    case ImageProcessorKind.ImageFlow:
        imageProcessingBuilder.UseImageFlow();
        break;
#endif
    default:
        throw new InvalidOperationException(
            $"ImageProcessing:Processor is \"{processor}\", but this build was compiled without that " +
            "processor (see the ImageProcessor MSBuild property in the .csproj). Rebuild with a matching " +
            "ImageProcessor value, or configure a processor this build includes.");
}

// Every source in ImageProcessing:Sources declares its route (RoutePrefix, PathPattern; bound into
// ImageProcessingOptions above) and the storage its originals come from, registered under its route prefix.
builder.Services.AddOptions<ImageProcessingOptions>()
    .Validate(
        options => options.Sources.Count > 0,
        "ImageProcessing:Sources must configure at least one source, e.g. Sources:0:RoutePrefix = \"/media\" " +
        "with Sources:0:Storage:Mode = \"LocalDisk\" | \"AzureBlob\" | \"HttpProxy\".")
    .ValidateOnStart();

foreach (IConfigurationSection source in imageProcessingSection.GetSection("Sources").GetChildren())
{
    string routePrefix = source["RoutePrefix"]
        ?? throw new InvalidOperationException($"ImageProcessing:Sources:{source.Key} has no RoutePrefix.");
    IConfigurationSection storage = source.GetSection("Storage");

    switch (storage.GetValue("Mode", ImageStorageMode.LocalDisk))
    {
        case ImageStorageMode.LocalDisk:
            imageProcessingBuilder.UseLocalDiskOriginalImageSource(routePrefix, options => storage.GetSection("LocalDisk").Bind(options));
            break;

        // Reads originals straight from the Blob container the owning app writes to — no shared
        // disk/volume between the two processes.
        case ImageStorageMode.AzureBlob:
            imageProcessingBuilder.UseAzureBlobOriginalImageSource(routePrefix, options => storage.GetSection("AzureBlob").Bind(options));
            break;

        // Fetches originals from Umbraco itself over HTTP, via the raw-original endpoint Umbraco
        // mounts at HttpOriginalImageSource.OriginRoutePrefix.
        case ImageStorageMode.HttpProxy:
            imageProcessingBuilder.UseHttpOriginalImageSource(routePrefix, options => storage.GetSection("HttpProxy").Bind(options));
            break;

        case ImageStorageMode mode:
            throw new InvalidOperationException($"ImageProcessing:Sources:{source.Key} has the unknown storage mode \"{mode}\".");
    }
}

WebApplication app = builder.Build();

// This entire app is the image service — Core's middleware is the whole pipeline, matched against
// ImageProcessing:Sources so the paths it serves line up with what the Umbraco sample's redirect
// middleware and ExternalBaseUrl-prefixed URLs both target.
app.UseImageProcessing();

// A visible link to the running code (AGPL §13 when built with the ImageFlow processor); also gives
// health probes a 200 at the root.
string sourceCodeUrl = app.Configuration.GetValue("SourceCodeUrl", "https://github.com/krebil/Umbraco.Image.Proccessing.Integrations")!;
app.MapGet("/", () => Results.Text($"Umbraco image processing service. Source code: {sourceCodeUrl}"));

await app.RunAsync();

/// <summary>
/// Which <c>IImageProcessor</c> this service registers. Swapping this is the same one-line,
/// config-driven story as the in-process sample.
/// </summary>
internal enum ImageProcessorKind
{
    SkiaSharp,
    ImageFlow,
}

/// <summary>
/// Where one source (<c>ImageProcessing:Sources:N:Storage:Mode</c>) resolves original (unprocessed) images
/// from. LocalDisk reads a directory (<c>Storage:LocalDisk:RootPath</c>), e.g. a disk/volume shared with
/// Umbraco; AzureBlob reads a Blob container (<c>Storage:AzureBlob</c>), e.g. the one Umbraco's media file
/// system writes to; HttpProxy fetches the raw original from Umbraco itself over HTTP (<c>Storage:HttpProxy</c>).
/// </summary>
internal enum ImageStorageMode
{
    LocalDisk,
    AzureBlob,
    HttpProxy,
}

// The compiler-generated top-level-statements Program class is already public in ASP.NET Core Web
// SDK projects, so integration tests can boot this app via WebApplicationFactory<Program> without an
// explicit partial class declaration here (ASP0027).
