using Umbraco.Image.Processing.AzureBlob.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.Service;
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
// ImageProcessingOptions above) and the storage its original images come from, registered under its
// route prefix. A source that would read the wrong place stops the service here, by name.
foreach (ConfiguredImageSource source in ImageSourceConfiguration.Read(imageProcessingSection))
{
    switch (source.Mode)
    {
        case ImageStorageMode.LocalDisk:
            imageProcessingBuilder.UseLocalDiskOriginalImageSource(source.RoutePrefix, options => source.Storage.GetSection("LocalDisk").Bind(options));
            break;

        // Reads original images straight from the Blob container the owning app writes to — no shared
        // disk/volume between the two processes.
        case ImageStorageMode.AzureBlob:
            imageProcessingBuilder.UseAzureBlobOriginalImageSource(source.RoutePrefix, options => source.Storage.GetSection("AzureBlob").Bind(options));
            break;

        // Fetches original images from Umbraco itself over HTTP, via the raw-original endpoint Umbraco
        // mounts at HttpOriginalImageSource.OriginRoutePrefix.
        case ImageStorageMode.HttpProxy:
            imageProcessingBuilder.UseHttpOriginalImageSource(source.RoutePrefix, options => source.Storage.GetSection("HttpProxy").Bind(options));
            break;
    }
}

WebApplication app = builder.Build();

foreach (string setting in ImageSourceConfiguration.SettingsNoLongerRead(imageProcessingSection))
{
    app.Logger.LogWarning("{Setting} is a 2.x setting this service no longer reads; its 3.0 form is in the README's 2.x to 3.0 mapping.", setting);
}

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

// The compiler-generated top-level-statements Program class is already public in ASP.NET Core Web
// SDK projects, so integration tests can boot this app via WebApplicationFactory<Program> without an
// explicit partial class declaration here (ASP0027).
