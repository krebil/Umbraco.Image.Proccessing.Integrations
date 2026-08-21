using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.ImageFlow;
using Umbraco.Image.Processing.SkiaSharp;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection imageProcessingSection = builder.Configuration.GetSection("ImageProcessing");
var processor = imageProcessingSection.GetValue<ImageProcessorKind?>("Processor")
    ?? throw new InvalidOperationException("ImageProcessing:Processor must be configured (e.g. \"SkiaSharp\" or \"ImageFlow\").");

IImageProcessingBuilder imageProcessingBuilder = builder.Services.AddImageProcessing(options => imageProcessingSection.Bind(options));

_ = processor switch
{
    ImageProcessorKind.ImageFlow => imageProcessingBuilder.UseImageFlow(),
    _ => imageProcessingBuilder.UseSkiaSharp(),
};

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
