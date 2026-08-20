# Plan: Self-Hosted ImageSharp Service, Deployed Separately from Umbraco

## Goal

Move image resizing/cropping out of the Umbraco/Delivery API process and into its own ASP.NET Core deployment, so the two can be scaled independently. Both apps read media from a shared Azure Blob Storage account; the image service is fronted by a CDN so most requests never reach either app. Umbraco keeps the `Umbraco.Cms.Imaging.ImageSharp` package and its middleware installed as normal — it's just never actually reached for real traffic, because an early redirect sends image requests under `/media` straight to the standalone service instead.

```
                         ┌─────────────────────┐
   Browser / client ───▶ │   CDN (Azure Front   │
                         │   Door / Azure CDN)   │
                         └──────────┬───────────┘
                   cache hit │              │ cache miss
                              │              ▼
                              │   ┌────────────────────────┐
                              │   │  images.example.com     │
                              │   │  standalone ImageSharp   │
                              │   │  ASP.NET Core service    │
                              │   └───────────┬─────────────┘
                              │               │ reads originals,
                              │               │ writes derivative cache
                              │               ▼
                              │   ┌────────────────────────┐
                              │   │  Azure Blob Storage      │
                              │   │  - media container       │
                              │   │  - imagesharpcache        │
                              │   │    container              │
                              │   └───────────┬─────────────┘
                              │               │ reads/writes media
                              ▼               ▼
                         ┌─────────────────────────┐
                         │  www.example.com          │
                         │  Umbraco + Delivery API    │
                         │  (ImageSharp middleware     │
                         │   still installed, but       │
                         │   unreachable — an early      │
                         │   redirect sends /media       │
                         │   image requests onward)      │
                         └─────────────────────────┘
```

---

## 1. Hosting platform for the image service — pros, cons, recommendation

You already have Umbraco on Azure. For the standalone image service, three realistic options:

### Azure App Service

**Pros**
- Same operational model your team likely already uses for the main Umbraco site — deployment slots, easy custom domains/TLS, VNet integration, familiar diagnostics.
- No cold starts: instances stay warm, so first-hit image requests are never slow.
- Dedicated CPU per instance on Premium v3 tiers, which suits the CPU-bound nature of resizing/encoding.

**Cons**
- No scale-to-zero — you pay for at least your minimum instance count even overnight when traffic is near zero.
- Scaling granularity is coarser (instance count on a fixed plan) rather than fine-grained concurrency/CPU-based autoscaling.

### Azure Container Apps

**Pros**
- Built on KEDA: can autoscale on HTTP concurrency, CPU, or queue depth, and can scale to zero when idle — a good match for an image tier that mostly sits behind a CDN and only gets bursts on cache misses or a cold cache after a deploy.
- Runs the exact same ASP.NET Core container/image you'd use anywhere else (no code changes vs. App Service), and revisions give you easy blue/green rollout.
- Materially cheaper for a spiky workload, since you don't pay for idle capacity.

**Cons**
- Runs on shared underlying infrastructure rather than dedicated VM capacity, so there's more performance variance than App Service Premium under sustained load.
- If you let it scale to zero, the very first request after an idle period pays a cold-start penalty. For an image host this mostly matters on cache misses, since the CDN absorbs the rest — mitigate by setting `minReplicas: 1` if that matters to you, which mostly cancels the scale-to-zero savings but keeps everything else.

### Azure Functions

**Not recommended for this.** As of the current (GA) ASP.NET Core Integration for the isolated worker model, Functions does **not** expose the ASP.NET Core middleware pipeline — you cannot call arbitrary `app.Use...()` middleware, which is exactly how `SixLabors.ImageSharp.Web` installs itself (`app.UseImageSharp()`). Hosting the actual ImageSharp.Web package on Functions isn't possible without rewriting its responsibilities (command parsing, provider abstraction, caching) by hand against the bare `SixLabors.ImageSharp` library inside a function body — at that point you're building your own image pipeline, not self-hosting ImageSharp.Web. Skip Functions for this.

### Recommendation

**Azure Container Apps**, for two reasons specific to this workload: it's genuinely CPU-bound and bursty (most requests are absorbed by the CDN; the app tier should be able to shrink to near-nothing between deploys/cache-busts and scale up fast when a new image size gets requested at volume), and it needs zero code changes vs. a plain ASP.NET Core app — same `Program.cs`, same Dockerfile you'd use on any container platform, so you're not locked in. If your team weighs operational simplicity and "no surprises on first request" above cost, App Service Premium v3 is the safe, boring alternative — same steps below, just deployed as a Web App instead of a Container App.

The plan below is written for Container Apps, with a note wherever App Service differs.

---

## 2. Shared storage: Azure Blob Storage

Both apps need to see the same media originals, and the image service's derivative cache should also live in Blob Storage rather than local disk — otherwise every Container App replica has its own cold cache and you lose most of the caching benefit whenever it scales out or a replica is replaced.

1. Create (or reuse) a Storage Account, and two containers:
   - `media` — the original uploaded assets, replacing local `wwwroot/media`.
   - `imagesharpcache` — ImageSharp.Web's derivative (resized/cropped) output cache.
2. Prefer a **managed identity** over an account key/connection string for both apps talking to the storage account — grant `Storage Blob Data Contributor` on `media` (Umbraco needs to write on upload) and on `imagesharpcache` (the image service needs to write derivatives), and `Storage Blob Data Reader` is enough for the image service on `media` if you want to tighten it further.
3. If the image service will read `media` directly by URL rather than through the app (it won't need to — see below), you'd need public/CDN-fronted blob access; you don't need that here, since the image service reads via the SDK, not by public blob URL.

---

## 3. Umbraco / Delivery API side changes

1. Install the storage packages and switch media off local disk:
   ```bash
   dotnet add package Umbraco.StorageProviders.AzureBlob
   dotnet add package Umbraco.StorageProviders.AzureBlob.ImageSharp
   ```
   `appsettings.json`:
   ```json
   "Umbraco": {
     "Storage": {
       "AzureBlob": {
         "Media": {
           "ConnectionString": "<managed-identity-or-connection-string>",
           "ContainerName": "media"
         }
       }
     }
   }
   ```
   `Program.cs`:
   ```csharp
   builder.CreateUmbracoBuilder()
       .AddBackOffice()
       .AddWebsite()
       .AddDeliveryApi()
       .AddComposers()
       .AddAzureBlobMediaFileSystem()
       // Still skip .AddAzureBlobImageSharpCache() — see step 2, the in-process
       // ImageSharp middleware is kept installed but never actually reached.
       .Build();
   ```

2. **Keep the `Umbraco.Cms.Imaging.ImageSharp` package installed — don't remove it.** Revised from the earlier version of this plan: rather than stripping the package out (which would also drop `ImageSharpImageUrlTokenGenerator` — the service that re-signs HMAC-signed image URLs embedded in rich text after a key rotation — and `ImageSharpDimensionExtractor`), leave `AddUmbracoImageSharp()` and its composer exactly as Umbraco registers them. `UseImageSharp()` middleware stays wired into the pipeline, `ImageSharpImageUrlGenerator` keeps producing ordinary relative `/media/...` URLs unchanged, HMAC signing keeps happening exactly as it does today (the stock `ImageSharpImageUrlGenerator` already signs when `HMACSecretKey` is configured — no custom generator needed). Nothing here needs to know the image host exists.

   Instead, stop *traffic* from ever reaching that middleware: add a redirect for image requests under `/media` early enough in the pipeline that it runs before Umbraco's own `UseImageSharp()` filter does. Since ASP.NET Core middleware runs in registration order, this just means adding it in `Program.cs` before the `app.UseUmbraco()...WithMiddleware(...)` block — no composer surgery, no touching `ImageSharpComposer` at all:

   ```csharp
   var app = builder.Build();

   // Umbraco's own supported raster types (Configuration.Default.ImageFormats) — redirect only
   // these; PDFs, docs, video etc. under /media fall through and keep being served by Umbraco
   // itself from blob storage, since the image service doesn't handle non-image passthrough.
   var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
   {
       ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif",
   };
   var imageHostBaseUrl = builder.Configuration["Imaging:RemoteHost"]!; // e.g. https://images.example.com

   app.Use(async (context, next) =>
   {
       if (context.Request.Path.StartsWithSegments("/media", out PathString remaining)
           && imageExtensions.Contains(Path.GetExtension(remaining.Value ?? string.Empty)))
       {
           var target = $"{imageHostBaseUrl}{context.Request.Path}{context.Request.QueryString}";
           context.Response.Headers.CacheControl = "public, max-age=31536000";
           context.Response.Redirect(target, permanent: false);
           return;
       }

       await next();
   });

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

   A few things worth being deliberate about here:
   - **Scope it to image extensions, not all of `/media`.** Umbraco's media library holds PDFs, documents, video — anything uploaded, not just images. The standalone service in §4 only has ImageSharp.Web wired up, with no fallback for non-image blobs, so a blanket `/media/*` redirect would 404 on anything that isn't a recognized raster image. Keep the extension list in sync with whatever `Configuration.Default.ImageFormats` actually supports if you ever add a codec.
   - **302, not 301, and explicit `Cache-Control`.** A 301 gets cached indefinitely by many browsers regardless of headers, which is a real liability if the image host's domain ever changes — you'd have no way to bust a client's cached redirect short of asking them to clear it. A 302 with an explicit long `Cache-Control` gives you the same practical caching benefit while keeping you in control of the lifetime.
   - **CORS, if anything fetches media via JS rather than `<img>` tags.** Plain `<img src>` doesn't care about cross-origin redirects, but a `fetch()`/`XMLHttpRequest` call expecting a same-origin response will fail CORS unless the image service adds `Access-Control-Allow-Origin` for your site's origin. Worth checking whether any part of the frontend does this before relying on the redirect for everything.
   - **The round trip is optional, not fundamental.** Every cache-miss request now costs a browser round trip to `www.example.com` before it's sent on to `images.example.com`. If that ever matters more than the simplicity of an app-level redirect, the same effect without the extra hop is to have your CDN do path-based origin routing instead (`www.example.com/media/*` proxied straight to the image service's origin, single canonical URL, no redirect at all) — worth revisiting once you have real traffic data, not something to build up front.

3. `IImageDimensionExtractor` (used for Image Cropper previews in the backoffice) keeps working automatically now that the package is still installed — nothing to change there.

---

## 4. Standalone image service

A brand-new ASP.NET Core project. It won't run Umbraco itself — no `IUmbracoBuilder`, no backoffice, no content database — but per the decision above it does take a package dependency on `Umbraco.Cms.Imaging.ImageSharp` purely to reuse `CropWebProcessor` without maintaining a copy of it.

1. **Create the project**
   ```bash
   dotnet new web -n Umbraco.ImageService
   dotnet add package SixLabors.ImageSharp.Web
   dotnet add package SixLabors.ImageSharp.Web.Providers.Azure
   ```

2. **Bring in `CropWebProcessor` via the `Umbraco.Cms.Imaging.ImageSharp` NuGet package.** Umbraco's Image Cropper emits crop coordinates and focal-point data via a custom querystring command (`cc`) that only `CropWebProcessor` understands — the stock `ResizeWebProcessor` ignores it. Without this processor registered, cropped/focal-point images will silently stop cropping once they're served by the new service.

   ```bash
   dotnet add package Umbraco.Cms.Imaging.ImageSharp
   ```

   This is a deliberate tradeoff, worth being explicit about: `Umbraco.Cms.Imaging.ImageSharp` declares a direct dependency on `Umbraco.Cms.Web.Common` (confirmed on nuget.org — version 17.4.2 requires `Umbraco.Cms.Web.Common >= 17.4.2 && < 18.0.0`), which per Umbraco's own dependency layering transitively pulls in Infrastructure, `PublishedCache.HybridCache`, and `Examine.Lucene` — none of which do anything in this service, since nothing initializes unless `Program.cs` calls `CreateUmbracoBuilder()...Build()` (which it won't). You're accepting a larger deployment (more assemblies restored and shipped in the container image) in exchange for never having to manually track upstream changes to `CropWebProcessor`'s cropping/EXIF-orientation logic — you just bump the package version alongside your main Umbraco upgrades. Two things to keep an eye on because of that choice:
   - **Version coupling**: this service's `Umbraco.Cms.Imaging.ImageSharp` version is now tied to the same major-version range as your main Umbraco site (e.g. pinned to `17.x`), so bumping Umbraco means bumping this service too, even though it never runs Umbraco itself.
   - **Container size / cold start**: if you go with the Container Apps `min-replicas: 0` option from §5, a heavier image means a slower first request after scale-from-zero — worth measuring once this is wired up, and switching to `min-replicas: 1` if it matters more than the cost saving.

   Then reference the type directly — no `IUmbracoBuilder` needed, it's just a class in the package:
   ```csharp
   using Umbraco.Cms.Imaging.ImageSharp.ImageProcessors;
   ```

3. **`Program.cs`**

   On processors: `AddImageSharp()` registers five stock processors by default — `ResizeWebProcessor`, `FormatWebProcessor`, `BackgroundColorWebProcessor`, `QualityWebProcessor`, `AutoOrientWebProcessor` — and the code below never calls `.ClearProcessors()`, so all five stay active exactly as they are on the current Umbraco site. `CropWebProcessor` is the *only* processor Umbraco itself adds on top of those defaults, so once it's registered (step 2) every processor Umbraco uses by default is accounted for — nothing else to port.

   What Umbraco *does* additionally configure, beyond registering processors, is the shared `Configuration` object the middleware runs against — most notably overriding the WebP encoder to lossy (ImageSharp 3.x defaults WebP to lossless, which produces ~10x larger files than the current site serves). That's not a processor, but it changes `format=webp` output, so it needs to come across too:

   ```csharp
   using Microsoft.AspNetCore.Http.Headers;
   using Microsoft.Net.Http.Headers;
   using SixLabors.ImageSharp.Formats.Webp;
   using SixLabors.ImageSharp.Web.Middleware;
   using SixLabors.ImageSharp.Web.Processors;
   using SixLabors.ImageSharp.Web.Providers.Azure;

   var builder = WebApplication.CreateBuilder(args);

   builder.Services.AddImageSharp()
       .ClearProviders()
       .Configure<AzureBlobStorageImageProviderOptions>(options =>
       {
           options.BlobContainers.Add(new AzureBlobContainerClientOptions
           {
               ConnectionString = builder.Configuration["Storage:ConnectionString"]!,
               ContainerName = "media",
           });
       })
       .AddProvider<AzureBlobStorageImageProvider>()
       .SetCache<AzureBlobStorageCache>()
       .Configure<AzureBlobStorageCacheOptions>(options =>
       {
           options.ConnectionString = builder.Configuration["Storage:ConnectionString"]!;
           options.ContainerName = "imagesharpcache";
       })
       // Registered last, same as Umbraco's own AddUmbracoImageSharp() does today —
       // preserves processor order so crop runs where Umbraco expects it to.
       .AddProcessor<CropWebProcessor>();

   builder.Services.Configure<ImageSharpMiddlewareOptions>(options =>
   {
       // Mirrors Umbraco.Cms.Imaging.ImageSharp's ConfigureImageSharpMiddlewareOptions.
       options.HMACSecretKey = Convert.FromBase64String(builder.Configuration["Imaging:HmacSecret"]!);
       options.BrowserMaxAge = TimeSpan.FromDays(7);
       options.CacheMaxAge = TimeSpan.FromDays(365);

       var maxWidth = builder.Configuration.GetValue<int>("Imaging:Resize:MaxWidth");
       var maxHeight = builder.Configuration.GetValue<int>("Imaging:Resize:MaxHeight");

       options.OnParseCommandsAsync = context =>
       {
           if (context.Commands.Count == 0)
           {
               return Task.CompletedTask;
           }

           if (context.Commands.Contains(ResizeWebProcessor.Width)
               && (!int.TryParse(context.Commands.GetValueOrDefault(ResizeWebProcessor.Width), out var width)
                   || width < 0 || width >= maxWidth))
           {
               context.Commands.Remove(ResizeWebProcessor.Width);
           }

           if (context.Commands.Contains(ResizeWebProcessor.Height)
               && (!int.TryParse(context.Commands.GetValueOrDefault(ResizeWebProcessor.Height), out var height)
                   || height < 0 || height >= maxHeight))
           {
               context.Commands.Remove(ResizeWebProcessor.Height);
           }

           return Task.CompletedTask;
       };

       options.OnPrepareResponseAsync = context =>
       {
           if (context.Request.Query.ContainsKey("rnd") || context.Request.Query.ContainsKey("v"))
           {
               ResponseHeaders headers = context.Response.GetTypedHeaders();
               CacheControlHeaderValue cacheControl = headers.CacheControl ?? new CacheControlHeaderValue { Public = true };
               cacheControl.MustRevalidate = false;
               cacheControl.Extensions.Add(new NameValueHeaderValue("immutable"));
               headers.CacheControl = cacheControl;
           }

           return Task.CompletedTask;
       };

       // Match the main site's output: ImageSharp 3.x defaults WebP to lossless, Umbraco overrides to lossy.
       options.Configuration.ImageFormatsManager.SetEncoder(
           WebpFormat.Instance,
           new WebpEncoder { FileFormat = WebpFileFormatType.Lossy });
   });

   var app = builder.Build();
   app.UseImageSharp();
   app.Run();
   ```

4. **Turn on HMAC URL signing.** Once this app is public on its own domain, anyone can request arbitrary resizes (`?width=99999`) unless you lock it down — `HMACSecretKey` above is what does that. Set the same secret in the Umbraco app's `Imaging:HMACSecretKey` setting (`ImagingSettings`) — the stock `ImageSharpImageUrlGenerator` already signs outgoing URLs with it when it's configured, so this works automatically now that the package stays installed; no custom generator needed. This also means `ImageSharpImageUrlTokenGenerator` (the service that re-signs image URLs embedded in rich text after a key rotation) keeps working unchanged, since it's part of the same package and was never removed.

5. **Dockerfile** (needed for Container Apps; also works for App Service for Containers if you go that route instead):
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
   WORKDIR /app
   EXPOSE 8080

   FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
   WORKDIR /src
   COPY . .
   RUN dotnet publish -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=build /app/publish .
   ENTRYPOINT ["dotnet", "Umbraco.ImageService.dll"]
   ```

---

## 5. Deploy the image service (Azure Container Apps)

1. Push the image to a registry (Azure Container Registry recommended, same subscription).
2. Create the Container App environment and app:
   ```bash
   az containerapp env create -g <rg> -n imgsvc-env --location <region>

   az containerapp create \
     -g <rg> -n imagesharp-svc \
     --environment imgsvc-env \
     --image <acr>.azurecr.io/umbraco-imageservice:latest \
     --target-port 8080 --ingress external \
     --min-replicas 0 --max-replicas 10 \
     --scale-rule-name http-scaler \
     --scale-rule-type http \
     --scale-rule-http-concurrency 50 \
     --cpu 1.0 --memory 2.0Gi \
     --user-assigned <managed-identity-resource-id>
   ```
3. Set `--min-replicas 1` instead of `0` if you'd rather pay for one always-warm replica and avoid any cold-start risk on cache misses — the main thing you keep either way is that this tier scales independently of the Umbraco app.
4. Bind the managed identity's Storage Blob Data Contributor role on the `imagesharpcache` container and Reader on `media` (see §2).
5. Map a custom domain (`images.example.com`) and certificate to the Container App.

*(App Service alternative: `az webapp create` with the same container image, a Premium v3 plan, and autoscale rules on CPU% instead of KEDA scale rules — everything else in this plan is unchanged.)*

---

## 6. CDN in front

1. Put Azure Front Door (or Azure CDN) in front of `images.example.com`, caching on the full querystring (width/height/crop/format are all in the querystring, so cache key must include it).
2. Respect the `Cache-Control`/`immutable` headers ImageSharp.Web already sets (`BrowserMaxAge`/`CacheMaxAge`, plus the `rnd`/`v` cache-buster handling ported from `ConfigureImageSharpMiddlewareOptions`) so the CDN and browsers cache aggressively and the origin (your Container App) only sees genuine cache misses.
3. This is what makes the scale-to-zero tradeoff acceptable: the CDN, not the image service, absorbs the vast majority of traffic.

---

## 7. Cutover checklist

1. Deploy the image service and CDN; confirm `https://images.example.com/media/xyz.jpg?width=300` resolves correctly and is HMAC-protected.
2. Deploy the Umbraco-side changes (blob media, the `/media` redirect middleware) to a staging slot first — verify rendered pages and Delivery API responses still emit ordinary `/media/...` URLs, and that browsers get redirected to `https://images.example.com/...` only for image requests, while a non-image asset (a PDF in the media library, say) still loads straight from Umbraco.
3. Warm the CDN/image cache for your most-requested crops before flipping production traffic, to avoid a stampede of cold-cache resizes on cutover.
4. Cut production over; watch the image service's autoscale behavior and 4xx/5xx rate for the first few hours.
5. Keep the old `wwwroot/media` around (don't delete) until you've confirmed the blob copy is complete and correct — this is a one-way migration only once you're sure.

---

## Open items to confirm before building this

- Confirmed: fresh site, default Umbraco Image Cropper only — no custom `IImageUrlGenerator`/processors to account for, which is why §4 only needs the stock `CropWebProcessor`.
- Umbraco version — build on ImageSharp 3.x from the start (needed for HMAC signing; 2.x doesn't support it, per `Umbraco.Cms.Imaging.ImageSharp2`'s compatibility notes), and pin the image service's `Umbraco.Cms.Imaging.ImageSharp` package to the same major version range.
- Expected image traffic pattern (steady vs. bursty) — confirms whether `min-replicas: 0` is actually worth it over `min-replicas: 1`.
