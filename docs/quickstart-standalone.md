# Quickstart: standalone image processing

This guide deploys image processing as its own ASP.NET Core service, separate
from Umbraco — so the two can scale independently, and image traffic never
has to reach the CMS process at all. It uses the same processor packages
(SkiaSharp or ImageFlow) and the same query-string command surface as the
in-process setup.

For running in the same process as Umbraco instead, see
[Quickstart: in-process image processing](quickstart-in-process.md).

## 1. Build the service

Create a bare ASP.NET Core project — no Umbraco reference at all. Add
references to Core and one processor project (see the in-process quickstart's
note on `dotnet add reference` vs `dotnet add package`: these projects aren't
published to NuGet yet):

```bash
dotnet new web -n MyCompany.ImageService
dotnet add reference path/to/Umbraco.Image.Processing.Core.csproj
dotnet add reference path/to/Umbraco.Image.Processing.SkiaSharp.csproj
```

`Program.cs` is small — Core's middleware *is* the whole app:

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
`<img>` URLs use — leave it as-is unless you have a reason to change it, since
the redirect middleware in step 4 assumes it lines up.

`OriginalsRootPath` must resolve to the same media files Umbraco serves. For
this proof-of-concept that means local disk reachable from both processes —
a shared volume, or (as this repo's own sample does for local dev) a relative
path across two checked-out projects. Azure Blob or another shared remote
store is real future work, not built here.

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

This does two things at once — no extra code beyond what's already in the
in-process quickstart's `Program.cs`, since both modes share the same
`AddImageProcessing()` call:

- **Freshly generated `<img>` URLs point straight at the standalone
  service.** `ImageProcessingOptions.ExternalBaseUrl` (bound here from
  `Standalone:BaseUrl` when unset) makes `IImageUrlGenerator` emit absolute
  URLs against the standalone host instead of a relative `/media/...` URL —
  so newly rendered pages skip a redirect round-trip entirely.
- **A redirect middleware catches everything else.** URLs that weren't
  generated with the host baked in — image links embedded in rich text,
  hand-typed URLs, anything hitting `/media` on the Umbraco app directly —
  still resolve, because Umbraco mounts a small middleware that 302-redirects
  matching requests to the standalone service. This mirrors the redirect
  pattern from `imagesharp-standalone-service-plan.md` §3, made
  processor-agnostic; no processor-specific code is needed on the Umbraco
  side for this to work; the middleware runs once, ahead of the CMS's own
  pipeline, and only in `Standalone` mode.

Umbraco keeps its normal image-generating code (`IImageUrlGenerator`) — you
are not hand-writing URLs. The mode switch changes what those calls produce,
not how you call them.

## 4. The drop-in story: swapping processors

Identical to the in-process case — only the `.UseSkiaSharp()` /
`.UseImageFlow()` call and the referenced processor project change:

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
  path and confirm you get a 302 to the standalone service — this is the
  fallback path for URLs the standalone host wasn't baked into.
- With `HmacSecretKey` set on both sides, confirm a tampered query string
  (change a digit in `width`) gets a 400 from the standalone service.

## Licensing note (ImageFlow only)

Running an image job through `Imageflow.NET`'s `InProcessAsync()` — exactly
what the ImageFlow processor does, standalone or in-process — requires
AGPLv3 compliance or a commercial Imazen license, independent of whether you
use `Imageflow.Server`. Confirm your license terms before shipping with
ImageFlow.
