using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.AzureBlob.Storage;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;
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
// ImageProcessingOptions above) and its own Storage. The storage is built when the middleware first
// asks for that route prefix, so configuration added after this point (e.g. by tests) still applies.
builder.Services.AddOptions<ImageProcessingOptions>()
    .Validate(
        options => options.Sources.Count > 0,
        "ImageProcessing:Sources must configure at least one source, e.g. Sources:0:RoutePrefix = \"/media\" " +
        "with Sources:0:Storage:Mode = \"LocalDisk\" | \"AzureBlob\" | \"HttpProxy\".")
    .ValidateOnStart();
builder.Services.AddHttpClient();
builder.Services.AddKeyedSingleton(KeyedService.AnyKey, (serviceProvider, key) => CreateOriginalImageSource(serviceProvider, (string)key!));

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

static IOriginalImageSource CreateOriginalImageSource(IServiceProvider serviceProvider, string routePrefix)
{
    IConfigurationSection source = serviceProvider.GetRequiredService<IConfiguration>()
        .GetSection("ImageProcessing:Sources")
        .GetChildren()
        .FirstOrDefault(s => ImageSourceMatcher.NormalizeRoutePrefix(s["RoutePrefix"]) == routePrefix)
        ?? throw new InvalidOperationException($"No ImageProcessing:Sources entry has the RoutePrefix \"{routePrefix}\".");
    IConfigurationSection storage = source.GetSection("Storage");

    return storage.GetValue("Mode", ImageStorageMode.LocalDisk) switch
    {
        ImageStorageMode.LocalDisk => new LocalDiskOriginalImageSource(
            storage.GetSection("LocalDisk").Get<LocalDiskOriginalImageSourceOptions>()?.RootPath ?? new LocalDiskOriginalImageSourceOptions().RootPath),

        // Reads originals straight from the Blob container the owning app writes to — no shared
        // disk/volume between the two processes.
        ImageStorageMode.AzureBlob => new AzureBlobOriginalImageSource(
            Options.Create(storage.GetSection("AzureBlob").Get<AzureBlobOriginalImageSourceOptions>() ?? new AzureBlobOriginalImageSourceOptions())),

        // Fetches originals from Umbraco itself over HTTP, via the raw-original endpoint Umbraco
        // mounts at HttpOriginalImageSource.OriginRoutePrefix.
        ImageStorageMode.HttpProxy => new HttpOriginalImageSource(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
            serviceProvider.GetRequiredService<IHmacSigner>(),
            Options.Create(storage.GetSection("HttpProxy").Get<HttpOriginalImageSourceOptions>() ?? new HttpOriginalImageSourceOptions())),

        ImageStorageMode mode => throw new InvalidOperationException($"Unknown ImageProcessing source storage mode \"{mode}\"."),
    };
}

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
