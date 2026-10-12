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
Core and one processor package. `UmbracoExtensions` isn't needed here — it
only implements Umbraco's own `IImageUrlGenerator`/`IImageDimensionExtractor`,
irrelevant to a standalone service that has no Umbraco dependency at all:

```bash
dotnet new web -n MyCompany.ImageService
dotnet add package Krebil.Umbraco.Image.Processing.Core
dotnet add package Krebil.Umbraco.Image.Processing.SkiaSharp
```

`Program.cs` stays small: Core's middleware *is* the whole app. Each
**source** is a route prefix plus the storage its originals come from; the
storage is registered under that route prefix.

```csharp
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Middleware;
using Umbraco.Image.Processing.SkiaSharp; // or .ImageFlow

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IImageProcessingBuilder imageProcessingBuilder = builder.Services
    .AddImageProcessing(options => builder.Configuration.GetSection("ImageProcessing").Bind(options));

imageProcessingBuilder.UseSkiaSharp(); // or .UseImageFlow()

imageProcessingBuilder.UseLocalDiskOriginalImageSource("/media", options =>
    options.RootPath = "<path or mount to the same media originals Umbraco reads>");

WebApplication app = builder.Build();

app.UseImageProcessing();

await app.RunAsync();
```

## 2. Configure it

```json
{
  "ImageProcessing": {
    "Sources": [
      { "RoutePrefix": "/media" }
    ],
    "DerivativeCacheRootPath": "App_Data/image-cache",
    "HmacSecretKey": "<same value as the Umbraco app's ImageProcessing:HmacSecretKey>"
  }
}
```

`Sources` lists the routes the service handles. Each entry has:

- `RoutePrefix` (required): the path prefix, e.g. `/media`, matching the path
  shape Umbraco's own `<img>` URLs use. Prefixes match whole path segments,
  case-insensitively, and the **longest** matching prefix wins. `"/"` matches
  every path.
- `PathPattern` (optional): a regular expression the path *after* the prefix
  (without its leading `/`) must match **in full**. Requests that don't match
  are not served by this source, and don't fall through to a shorter prefix
  either, so it works as an access gate on the storage behind the source.

Paths with a `.` or `..` segment (split on `/` or `\`), or with a
percent-encoded separator (`%2F`, `%5C`) still in them, are refused with a
400 by every source, so a request can't resolve into another source's
storage. They are refused rather than passed on: in-process, the next
handler would be static files, which would serve the raw file. A
`PathPattern` that times out (100 ms) is refused with a 404 and logged as a
warning, because it points at a pattern to fix.

Storage is registered in code per route prefix
(`UseLocalDiskOriginalImageSource`, `UseAzureBlobOriginalImageSource`,
`UseHttpOriginalImageSource`, or `AddOriginalImageSource` with your own
`IOriginalImageSource`). A source without a registration of its own reads from
the default `IOriginalImageSource` (local disk at `OriginalsRootPath`).

The derivative cache is shared by all sources; its keys include the route
prefix, so the same relative path under two prefixes never collides. The HMAC
signature covers the full request path, prefix included.

The reference service in this repo (`Umbraco.Image.Processing.Service`) reads
each source's storage from configuration instead, under
`ImageProcessing:Sources:N:Storage`:

```json
{
  "ImageProcessing": {
    "Processor": "ImageFlow",
    "Sources": [
      {
        "RoutePrefix": "/media",
        "Storage": {
          "Mode": "LocalDisk",
          "LocalDisk": { "RootPath": "<path to the media originals>" }
        }
      }
    ]
  }
}
```

`Storage:Mode` is `LocalDisk` (`Storage:LocalDisk:RootPath`), `AzureBlob`
(`Storage:AzureBlob:ConnectionString`/`ContainerName`/`BlobPathPrefix`) or
`HttpProxy` (`Storage:HttpProxy:UmbracoBaseUrl`). A single source may leave
`Storage:Mode` out and gets `LocalDisk` at `wwwroot/media`, as in 2.x. With
several sources, each must set `Storage:Mode` and the settings that mode
needs (`LocalDisk:RootPath`; `AzureBlob:ConnectionString` and
`ContainerName`; `HttpProxy:UmbracoBaseUrl`), so no source quietly reads
another's default; `AzureBlob:ConnectionString` and `HttpProxy:UmbracoBaseUrl`
are required in any case. The service refuses to start otherwise, and
without at least one source, naming the source by index and prefix. A
leftover 2.x setting (`ImageProcessing:RoutePrefix`, `Storage`, `Proxy`,
`OriginalsRootPath`) is logged as a warning at start-up. Its root (`/`)
answers with a link to its source code (`SourceCodeUrl`, defaulting to this
repository).

Local-disk storage must resolve to the same media files Umbraco serves. It
needs local disk reachable from both processes — a shared volume, or (as this
repo's own sample does for local dev) a relative path across two checked-out
projects. It only works while Umbraco and the standalone service share a
filesystem, which stops being true once they're genuinely separate
deployments (see `imagesharp-standalone-service-plan.md` §2). For that case,
use Blob mode instead.

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

On the standalone service side, register the Blob implementation for the
source instead of local disk:

```csharp
using Umbraco.Image.Processing.AzureBlob.DependencyInjection;

imageProcessingBuilder.UseAzureBlobOriginalImageSource("/media", options =>
    builder.Configuration.GetSection("ImageProcessing:Sources:0:Storage:AzureBlob").Bind(options));
```

```json
{
  "ImageProcessing": {
    "Sources": [
      {
        "RoutePrefix": "/media",
        "Storage": {
          "Mode": "AzureBlob",
          "AzureBlob": {
            "ConnectionString": "<same connection string as Umbraco's Media file system, above>",
            "ContainerName": "media",
            "BlobPathPrefix": "media"
          }
        }
      }
    ]
  }
}
```

Both sides must point at the **same** connection string and container.
`ContainerName` here is the container Umbraco's media file system writes to.
The blob name is `BlobPathPrefix` plus the request path after the route
prefix. `BlobPathPrefix` defaults to `"media"`, matching
`Umbraco.StorageProviders.AzureBlob`'s own default blob naming; only change it
if you've overridden `ContainerRootPath`/`VirtualPath` on the Umbraco side.

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

### Several sources: serving a second container

One instance can serve images from several places. Add a source per route
prefix, each with its own storage. For example, Umbraco media plus
content-addressed assets that another part of the app writes to a private
`archive` container under `sites/{site}/assets/{hash}.{ext}`:

```json
{
  "ImageProcessing": {
    "Sources": [
      {
        "RoutePrefix": "/media",
        "Storage": { "Mode": "AzureBlob", "AzureBlob": { "ConnectionString": "<...>", "ContainerName": "media", "BlobPathPrefix": "media" } }
      },
      {
        "RoutePrefix": "/-/archive",
        "PathPattern": "^[0-9a-f]{32}/assets/[0-9a-f]{32}\\.[a-z0-9]{1,10}$",
        "Storage": { "Mode": "AzureBlob", "AzureBlob": { "ConnectionString": "<...>", "ContainerName": "archive", "BlobPathPrefix": "sites" } }
      }
    ]
  }
}
```

`/-/archive/{site}/assets/{hash}.png?width=400` then reads the blob
`archive:sites/{site}/assets/{hash}.png`, and the `PathPattern` keeps every
other blob in that container out of reach. As environment variables, the same
settings are `ImageProcessing__Sources__1__RoutePrefix`,
`ImageProcessing__Sources__1__Storage__AzureBlob__ContainerName` and so on.

The Umbraco app needs the same `Sources` list (route prefixes and patterns;
storage is ignored there) so its URL generator and redirect middleware (step 3)
send exactly these paths to the service.

### HTTP-proxy mode: fetching originals from Umbraco over HTTP

If Umbraco's media is plain local disk *and* Umbraco and the standalone
service are genuinely separate deployments with no shared disk/volume,
neither local-disk nor Blob mode applies. Use this third mode
instead: the service asks Umbraco itself for the raw file over HTTP.

On the Umbraco side, mount a raw-original endpoint — same idea as step 3's
redirect middleware below: a small piece of glue code in your own
`Program.cs`, not a call into a packaged method, since it's specific to how
your app is deployed. It's served at a path that deliberately does **not**
nest under any source's `RoutePrefix`, so it's exempt from that redirect
middleware — a naive request under a source's prefix would otherwise bounce
straight back to the service that made it. The service sends the source's
full path after that prefix (`/__image-origin/media/1234/photo.jpg`), so
the endpoint runs the same `ImageSourceMatcher` to pick the source, which
also applies its `PathPattern`, and reads from the storage registered for
that source (or the default one):

```csharp
using Umbraco.Image.Processing.Core.Commands;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Routing;
using Umbraco.Image.Processing.Core.Security;
using Umbraco.Image.Processing.Core.Storage;

app.MapGet($"{HttpOriginalImageSource.OriginRoutePrefix}/{{**path}}", async (
    HttpContext context,
    ImageSourceMatcher sourceMatcher,
    IHmacSigner hmacSigner) =>
{
    if (!hmacSigner.Validate(context.Request.Path, context.Request.Query, context.Request.Query[ImageProcessingCommandNames.HmacToken]))
    {
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    var sourcePath = new PathString("/" + ((string?)context.GetRouteValue("path") ?? string.Empty));
    if (!sourceMatcher.TryMatch(sourcePath, out ImageSourceMatch? match))
    {
        return Results.NotFound();
    }

    IOriginalImageSource originalImageSource = context.RequestServices.GetOriginalImageSource(match.RoutePrefix);
    Stream? source = await originalImageSource.OpenReadAsync(match.RelativePath, context.RequestAborted);
    return source is null ? Results.NotFound() : Results.Stream(source, "application/octet-stream");
});
```

`ImageSourceMatcher`, `IHmacSigner` and the sources `GetOriginalImageSource`
looks up all resolve from the same DI registrations `AddImageProcessing()`
already sets up for your Umbraco app's own in-process image handling — no
separate wiring needed, and safe to mount unconditionally regardless of
`ImageProcessing:Mode`, since it costs nothing when nothing calls it and
carries the same HMAC guard as the rest of your image requests. Two
HTTP-proxy sources therefore never collide: `/media/x.jpg` and
`/-/archive/x.jpg` reach the endpoint as two different paths and resolve to
two different storages.

On the standalone service side, register the HTTP implementation for the
source instead of local disk:

```csharp
using Umbraco.Image.Processing.Core.DependencyInjection;

imageProcessingBuilder.UseHttpOriginalImageSource("/media", options =>
    builder.Configuration.GetSection("ImageProcessing:Sources:0:Storage:HttpProxy").Bind(options));
```

```json
{
  "ImageProcessing": {
    "Sources": [
      {
        "RoutePrefix": "/media",
        "Storage": {
          "Mode": "HttpProxy",
          "HttpProxy": {
            "UmbracoBaseUrl": "<Umbraco's own internal base URL, reachable from the service>"
          }
        }
      }
    ]
  }
}
```

Umbraco's endpoint receives the source's full path, route prefix included
(`HttpOriginalImageSourceOptions.RoutePrefix`, which this overload sets to
the prefix it registers for), and resolves it with the source's own
`IOriginalImageSource` on its side. The two apps therefore need the same
`Sources` list, as the "Several sources" section says.

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
rendered pages skip a redirect round-trip entirely. Only paths a configured
source serves (`ImageProcessing:Sources`, or `/media` when none is set) get
the host; anything else stays relative.

Second, a redirect middleware catches everything else. URLs that weren't
generated with the host baked in (image links embedded in rich text,
hand-typed URLs, anything hitting `/media` on the Umbraco app directly) still
resolve, because Umbraco mounts a small middleware that 302-redirects matching
requests to the standalone service. It uses `ImageSourceMatcher`, the same
longest-prefix, extension and `PathPattern` rules as the service, so it
redirects exactly the paths the service would serve. This mirrors the redirect pattern from
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

## Scheduling eviction

`IDerivativeImageCache.EvictExpiredAsync` removes entries older than
`CacheControlMaxAge`. Nothing in this repo calls it automatically — that's a
deliberate choice, not an oversight (production-hardening ticket 13). Reads
already filter expired entries on their own (`TryOpenReadAsync` never serves
a stale one), so skipping eviction doesn't risk correctness, only unbounded
disk/Blob growth over time.

Wire `EvictExpiredAsync` into whatever trigger fits your deployment: an
endpoint you add and call from an external scheduler (a Kubernetes
`CronJob`, an Azure Function Timer, a cloud scheduler), a `BackgroundService`
if you'd rather have an always-on process own it, or anything else — this
repo doesn't prescribe a mechanism, the same way it doesn't prescribe your
rate limiting or secret management.

If you're hosting this service somewhere that scales to zero (Azure
Container Apps, Cloud Run, and similar), an HTTP trigger has a second
benefit: the scheduler's call is also what wakes the app to run the pass.
That's part of why this repo doesn't ship an in-process timer here — an
always-warm background timer would keep the process alive permanently and
defeat scale-to-zero outright.

Local-disk eviction (a directory walk) and Blob eviction
(`AzureBlobDerivativeImageCache`, a full `GetBlobsAsync` container listing
with real API cost) may reasonably run on different cadences — that's your
call per backend, not a single fixed answer this repo provides.

## Licensing note (ImageFlow only)

Running an image job through `Imageflow.NET`'s `InProcessAsync()` (exactly
what the ImageFlow processor does, standalone or in-process) requires AGPLv3
compliance or a commercial Imazen license, independent of whether you use
`Imageflow.Server`. Confirm your license terms before shipping with
ImageFlow.
