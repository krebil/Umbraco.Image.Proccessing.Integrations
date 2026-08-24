using Umbraco.Image.Processing.AzureBlob.DependencyInjection;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.ImageFlow;
using Umbraco.Image.Processing.SkiaSharp;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection imageProcessingSection = builder.Configuration.GetSection("ImageProcessing");
var processor = imageProcessingSection.GetValue<ImageProcessorKind?>("Processor")
    ?? throw new InvalidOperationException("ImageProcessing:Processor must be configured (e.g. \"SkiaSharp\" or \"ImageFlow\").");
var storageMode = imageProcessingSection.GetValue("Storage:Mode", ImageStorageMode.LocalDisk);

IImageProcessingBuilder imageProcessingBuilder = builder.Services.AddImageProcessing(options => imageProcessingSection.Bind(options));

_ = processor switch
{
    ImageProcessorKind.ImageFlow => imageProcessingBuilder.UseImageFlow(),
    _ => imageProcessingBuilder.UseSkiaSharp(),
};

if (storageMode == ImageStorageMode.AzureBlob)
{
    // Reads originals straight from the same Blob container Umbraco's own Blob-backed media file
    // system (Umbraco.StorageProviders.AzureBlob) writes to — no shared disk/volume between the two
    // processes, unlike LocalDisk mode's OriginalsRootPath convention (production-hardening ticket 11).
    imageProcessingBuilder.UseAzureBlobOriginalImageSource(options =>
        imageProcessingSection.GetSection("Storage:AzureBlob").Bind(options));
}
else if (storageMode == ImageStorageMode.HttpProxy)
{
    // Neither a shared disk nor Blob-backed media applies: fetches originals from Umbraco itself over
    // HTTP, via the raw-original endpoint Umbraco mounts unconditionally at HttpOriginalImageSource's
    // own OriginRoutePrefix (production-hardening ticket 12).
    imageProcessingBuilder.UseHttpOriginalImageSource(options =>
        imageProcessingSection.GetSection("Proxy").Bind(options));
}

WebApplication app = builder.Build();

// This entire app is the image service — Core's middleware is the whole pipeline, matched by
// ImageProcessingOptions.RoutePrefix (still "/media" by default) so the paths it serves line up
// with what the Umbraco sample's redirect middleware and ExternalBaseUrl-prefixed URLs both target.
app.UseImageProcessing();

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
/// Where this service resolves original (unprocessed) media from. LocalDisk expects a shared
/// disk/volume with Umbraco (<see cref="Umbraco.Image.Processing.Core.Options.ImageProcessingOptions.OriginalsRootPath" />);
/// AzureBlob expects Umbraco's media file system to also be Blob-backed, at the same container;
/// HttpProxy expects neither — it fetches the raw original from Umbraco itself over HTTP.
/// </summary>
internal enum ImageStorageMode
{
    LocalDisk,
    AzureBlob,
    HttpProxy,
}

/// <summary>
/// Marker so integration tests can boot this app via <c>WebApplicationFactory&lt;Program&gt;</c> —
/// top-level statements otherwise generate an internal, inaccessible <c>Program</c> class.
/// </summary>
public partial class Program;
