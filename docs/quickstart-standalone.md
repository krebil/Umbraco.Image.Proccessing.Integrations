# Quickstart: standalone image processing

This guide deploys image processing as its own ASP.NET Core service, separate
from Umbraco. That way the two can scale independently, and image traffic
never has to reach the CMS process at all. It uses the same processor packages
(SkiaSharp or ImageFlow) and the same query-string command surface as the
in-process setup.

For running in the same process as Umbraco instead, see
[Quickstart: in-process image processing](quickstart-in-process.md).

## 1. Build the service

Create a bare ASP.NET Core project with no Umbraco reference at all. Add
references to Core and one processor project (see the in-process quickstart's
note on `dotnet add reference` vs `dotnet add package`: these projects aren't
published to NuGet yet):

```bash
dotnet new web -n MyCompany.ImageService
dotnet add reference path/to/Umbraco.Image.Processing.Core.csproj
dotnet add reference path/to/Umbraco.Image.Processing.SkiaSharp.csproj
```

`Program.cs` stays small: Core's middleware *is* the whole app.

```csharp
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.SkiaSharp; // or .ImageFlow

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IImageProcessingBuilder imageProcessingBuilder = builder.Services
    .AddImageProcessing(options => builder.Configuration.GetSection("ImageProcessing").Bind(options));

imageProcessingBuilder.UseSkiaSharp(); // or .UseImageFlow()

WebApplication app = builder.Build();

app.UseImageProcessing();

await app.RunAsync();
```

## 2. Configure it

```json
{
  "ImageProcessing": {
    "Processor": "SkiaSharp",
    "OriginalsRootPath": "<path or mount to the same media originals Umbraco reads>",
    "DerivativeCacheRootPath": "App_Data/image-cache",
    "HmacSecretKey": "<same value as the Umbraco app's ImageProcessing:HmacSecretKey>"
  }
}
```

`RoutePrefix` defaults to `/media`, matching the path shape Umbraco's own
`<img>` URLs use. Leave it as-is unless you have a reason to change it, since
the redirect middleware in step 4 assumes it lines up.

`OriginalsRootPath` must resolve to the same media files Umbraco serves. This
is local-disk mode: it needs local disk reachable from both processes — a
shared volume, or (as this repo's own sample does for local dev) a relative
path across two checked-out projects. It only works while Umbraco and the
standalone service share a filesystem, which stops being true once they're
genuinely separate deployments (see `imagesharp-standalone-service-plan.md`
§2). For that case, use Blob mode instead.

### Blob mode: resolving originals from Azure Blob Storage

If Umbraco's own media is Blob-backed instead of local disk, point the
standalone service at the same container directly — no shared disk between
the two processes at all.

On the Umbraco side, add the `Umbraco.StorageProviders.AzureBlob` package and
wire its media file system:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddAzureBlobMediaFileSystem()
    .Build();
```

```json
{
  "Umbraco": {
    "Storage": {
      "AzureBlob": {
        "Media": {
          "ConnectionString": "<same connection string as the service, below>",
          "ContainerName": "media"
        }
      }
    }
  }
}
```

On the standalone service side, swap `IOriginalImageSource` to the Blob
implementation instead of setting `OriginalsRootPath`:

```csharp
using Umbraco.Image.Processing.AzureBlob.DependencyInjection;

imageProcessingBuilder.UseAzureBlobOriginalImageSource(options =>
    builder.Configuration.GetSection("ImageProcessing:Storage:AzureBlob").Bind(options));
```

```json
{
  "ImageProcessing": {
    "Storage": {
      "AzureBlob": {
        "ConnectionString": "<same connection string as Umbraco's Media file system, above>",
        "ContainerName": "media",
        "BlobPathPrefix": "media"
      }
    }
  }
}
```

Both sides must point at the **same** connection string and container.
`ContainerName` here is the container Umbraco's media file system writes to.
`BlobPathPrefix` defaults to `"media"`, matching `Umbraco.StorageProviders.AzureBlob`'s
own default blob naming; only change it if you've overridden
`ContainerRootPath`/`VirtualPath` on the Umbraco side.

If you also enable `AzureBlobDerivativeImageCache` (ticket 05) for the
derivative cache, it can share the **same storage account** (the same
`ConnectionString`) as this — but keep it in its **own container**
(`AzureBlobCacheOptions.ContainerName`, default `"image-derivative-cache"`),
not this one. `ClearAsync`/`EvictExpiredAsync` enumerate and delete
everything in the cache's container, which would be unsafe to run against a
container that also holds Umbraco's real media.

Unlike the derivative cache, this container is **not created automatically**
— its lifecycle belongs to Umbraco's media file system. Provision it yourself
ahead of deploy, the same way you would for a real Azure Storage account. A
missing container surfaces as every request 404ing, not a startup failure.

### HTTP-proxy mode: fetching originals from Umbraco over HTTP

If Umbraco's media is plain local disk *and* Umbraco and the standalone
service are genuinely separate deployments with no shared disk/volume,
neither `OriginalsRootPath` nor Blob mode applies. Use this third mode
instead: the service asks Umbraco itself for the raw file over HTTP.

On the Umbraco side, mount a raw-original endpoint — same idea as step 3's
redirect middleware below: a small piece of glue code in your own
`Program.cs`, not a call into a packaged method, since it's specific to how
your app is deployed. It's served at a path that deliberately does **not**
nest under `RoutePrefix`, so it's exempt from that redirect middleware — a
naive request under `RoutePrefix` would otherwise bounce straight back to the
service that made it:

```csharp
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

app.MapGet($"{HttpOriginalImageSource.OriginRoutePrefix}/{{**path}}", async (
    HttpContext context,
    IOriginalImageSource originalImageSource,
    IHmacSigner hmacSigner) =>
{
    if (!hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
    {
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    string relativePath = "/" + ((string?)context.GetRouteValue("path") ?? string.Empty);
    Stream? source = await originalImageSource.OpenReadAsync(relativePath, context.RequestAborted);
    return source is null ? Results.NotFound() : Results.Stream(source, "application/octet-stream");
});
```

`IOriginalImageSource` and `IHmacSigner` here resolve from the same DI
registrations `AddImageProcessing()` already sets up for your Umbraco app's
own in-process image handling — no separate wiring needed, and safe to mount
unconditionally regardless of `ImageProcessing:Mode`, since it costs nothing
when nothing calls it and carries the same HMAC guard as the rest of your
image requests.

On the standalone service side, swap `IOriginalImageSource` to the HTTP
implementation instead of setting `OriginalsRootPath`:

```csharp
using Umbraco.Image.Processing.Core.DependencyInjection;

imageProcessingBuilder.UseHttpOriginalImageSource(options =>
    builder.Configuration.GetSection("ImageProcessing:Proxy").Bind(options));
```

```json
{
  "ImageProcessing": {
    "Proxy": {
      "UmbracoBaseUrl": "<Umbraco's own internal base URL, reachable from the service>"
    }
  }
}
```

`UmbracoBaseUrl` points the opposite direction from `Standalone:BaseUrl`
below: that one tells Umbraco where the service is, this one tells the
service where Umbraco is. They're independent settings and commonly resolve
to different hosts (public vs. internal).

The request Umbraco receives is guarded by the same `HmacSecretKey` used
everywhere else in this guide — the service signs its own outbound request
with it, and Umbraco's endpoint validates the signature the same way it
validates any other image request. There's no separate secret to configure.

### Sharing the HMAC secret

Both apps need the **same** `HmacSecretKey`: the Umbraco app signs URLs with
it, and the standalone service verifies the signature. Set it identically in
both apps' configuration (and set `Umbraco:CMS:Imaging:HMACSecretKey` on the
Umbraco side too, so Umbraco's own `<img>`/`<picture>` helpers sign with the
matching key). A request with a missing or tampered signature gets a 400 from
the service once signing is enabled.

## 3. Point the Umbraco app at it

On the Umbraco side, set:

```json
{
  "ImageProcessing": {
    "Mode": "Standalone",
    "Standalone": {
      "BaseUrl": "https://images.example.com"
    }
  }
}
```

This does two things at once, with no extra code beyond what's already in the
in-process quickstart's `Program.cs`, since both modes share the same
`AddImageProcessing()` call.

First, freshly generated `<img>` URLs point straight at the standalone
service. `ImageProcessingOptions.ExternalBaseUrl` (bound here from
`Standalone:BaseUrl` when unset) makes `IImageUrlGenerator` emit absolute URLs
against the standalone host instead of a relative `/media/...` URL, so newly
rendered pages skip a redirect round-trip entirely.

Second, a redirect middleware catches everything else. URLs that weren't
generated with the host baked in (image links embedded in rich text,
hand-typed URLs, anything hitting `/media` on the Umbraco app directly) still
resolve, because Umbraco mounts a small middleware that 302-redirects matching
requests to the standalone service. This mirrors the redirect pattern from
`imagesharp-standalone-service-plan.md` §3, made processor-agnostic. No
processor-specific code is needed on the Umbraco side for this to work; the
middleware just runs once, ahead of the CMS's own pipeline, and only in
`Standalone` mode.

Umbraco keeps its normal image-generating code (`IImageUrlGenerator`), so
you're not hand-writing URLs. The mode switch changes what those calls
produce, not how you call them.

## 4. The drop-in story: swapping processors

Identical to the in-process case: only the `.UseSkiaSharp()` /
`.UseImageFlow()` call and the referenced processor project change.

```csharp
imageProcessingBuilder.UseSkiaSharp();
// or
imageProcessingBuilder.UseImageFlow();
```

Same format-support caveat as in-process: SkiaSharp encodes
`jpg`/`jpeg`/`png`/`webp` only; ImageFlow adds `gif`; neither supports `bmp`.

## 5. Verify it

With the standalone service running and the Umbraco app in `Standalone` mode:

- Request an image directly from the service:
  `GET https://images.example.com/media/<your-image>.jpg?width=400`.
- Load a page rendered by Umbraco and confirm its `<img>` URLs point straight
  at `https://images.example.com/...` (no redirect needed).
- Request the same image directly from the Umbraco app's own `/media/...`
  path and confirm you get a 302 to the standalone service. This is the
  fallback path for URLs the standalone host wasn't baked into.
- With `HmacSecretKey` set on both sides, confirm a tampered query string
  (change a digit in `width`) gets a 400 from the standalone service.

## Licensing note (ImageFlow only)

Running an image job through `Imageflow.NET`'s `InProcessAsync()` (exactly
what the ImageFlow processor does, standalone or in-process) requires AGPLv3
compliance or a commercial Imazen license, independent of whether you use
`Imageflow.Server`. Confirm your license terms before shipping with
ImageFlow.
