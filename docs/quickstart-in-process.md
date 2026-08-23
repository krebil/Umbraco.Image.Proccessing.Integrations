# Quickstart: in-process image processing

This guide adds pluggable image processing to an existing Umbraco site, running
in the same process as Umbraco itself. It replaces `Umbraco.Cms.Imaging.ImageSharp`
with a processor you choose (SkiaSharp or ImageFlow), while keeping the same
query-string command surface: `width`, `height`, `format`, `quality`, `bgcolor`,
autoorient, and Umbraco's `cc` crop/focal-point command.

For a separately deployed image service instead, see
[Quickstart: standalone image processing](quickstart-standalone.md).

## 1. Add the packages

The processor packages aren't published to NuGet yet (this is a proof-of-concept
abstraction, not a released library), so reference the projects directly:

```bash
dotnet add reference path/to/Umbraco.Image.Processing.Core.csproj
dotnet add reference path/to/Umbraco.Image.Processing.SkiaSharp.csproj
```

(Swap the last line for `Umbraco.Image.Processing.ImageFlow.csproj` if you're
starting with ImageFlow. Reference both if you want the config-only swap
described in step 4, the way this repo's own sample site does it.)

## 2. Remove the stock ImageSharp package

Drop the `Umbraco.Cms.Imaging.ImageSharp` package reference (or
`Umbraco.Cms.Imaging.ImageSharp2`, if you're on that variant). It registers its
own `IImageUrlGenerator`/`IImageDimensionExtractor`, which the processing
package replaces at startup regardless. Still worth removing, though, so you're
not shipping an imaging pipeline you don't use.

## 3. Register it in `Program.cs`

```csharp
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.SkiaSharp; // or .ImageFlow

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Register AFTER CreateUmbracoBuilder(), not before: Umbraco's own imaging
// package registers a default IImageUrlGenerator/IImageDimensionExtractor via a
// plain Add, and DI's last-one-wins resolution picks whichever registration
// runs last regardless of call order. AddImageProcessing() replaces Umbraco's
// registrations outright, so it must run after CreateUmbracoBuilder() to win.
builder.Services
    .AddImageProcessing(options => builder.Configuration.GetSection("ImageProcessing").Bind(options))
    .UseSkiaSharp(); // or .UseImageFlow()

WebApplication app = builder.Build();

// Mount BEFORE Umbraco's own pipeline: the middleware serves media requests
// (resized, cropped, or passed through) directly, so Umbraco's static file
// handling never sees them.
app.UseImageProcessing();

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
```

## 4. Configure it

```json
{
  "ImageProcessing": {
    "RoutePrefix": "/media",
    "OriginalsRootPath": "wwwroot/media",
    "DerivativeCacheRootPath": "App_Data/image-cache",
    "HmacSecretKey": "<a base64-encoded random key, or omit to disable signing>"
  }
}
```

All settings have working defaults (shown above) except `HmacSecretKey`. That
one's unset by default, so signing and verification stay disabled until you
set it yourself. If you enable it, also set `Umbraco:CMS:Imaging:HMACSecretKey`
to the same value, so Umbraco's own `<img>`/`<picture>` helpers sign URLs the
middleware will accept.

`OriginalsRootPath` and `DerivativeCacheRootPath` are local-disk paths for this
proof-of-concept. There's no Azure Blob or other remote storage backend yet.

## 5. The drop-in story: swapping processors

Nothing above names a processor except the one `.UseSkiaSharp()` /
`.UseImageFlow()` call in step 3 and the package reference it depends on.
Compare all three:

```csharp
// Stock Umbraco:
services.AddUmbracoImageSharp();

// This package, SkiaSharp:
services.AddImageProcessing(configure).UseSkiaSharp();

// This package, ImageFlow:
services.AddImageProcessing(configure).UseImageFlow();
```

Everything else in `Program.cs` (registration order, middleware mount point,
options binding) stays identical. Swapping processors is a one-line change
plus swapping which project you reference.

Format support differs by processor, though. SkiaSharp's encoder only handles
`jpg`/`jpeg`/`png`/`webp`, so a `format=gif` or `format=bmp` request throws.
ImageFlow additionally supports `gif`; neither supports `bmp`. Pick a processor
that covers the output formats your site actually needs.

## 6. Verify it

Request an image through the middleware's route prefix with a resize command:

```
GET /media/<your-image>.jpg?width=400
```

You should get back a 400px-wide image, and a cached derivative should appear
under `DerivativeCacheRootPath`. Try `cc=0.25,0.25,0.25,0.25&width=300&height=300`
for a centered crop, and `format=webp` for a format conversion.

## Licensing note (ImageFlow only)

Running an image job through `Imageflow.NET`'s `InProcessAsync()` (exactly
what the ImageFlow processor does) requires AGPLv3 compliance or a commercial
Imazen license, independent of whether you use `Imageflow.Server`. Confirm
your license terms before shipping with ImageFlow.
