Type: task
Status: resolved

## Question

Build out `src/Umbraco.Image.Processing.Core` (currently an empty scaffold) as the shared foundation every processor project and both sample hosts depend on:

- A canonical command model representing the parsed, normalized query-string surface: `width`, `height`, `format`, `quality`, `bgcolor`, autoorient, and Umbraco's `cc` crop/focal-point command.
- An `IImageProcessor` interface (or equivalent) that a processor project implements: normalized input (bytes + canonical command) → output bytes. This is the seam SkiaSharp and ImageFlow plug into.
- Middleware that parses and validates the query string into the canonical command model (mirroring the plan doc's `OnParseCommandsAsync` width/height clamping), invokes the active `IImageProcessor`, and writes the response — usable from both in-process (mounted into the existing Umbraco pipeline) and standalone (mounted into a bare ASP.NET Core app) hosts.
- HMAC signing/verification, implemented once, shared by every processor and both deployment modes.
- The single shared `IImageUrlGenerator` + dimension-extractor implementation that produces Umbraco's existing `/media/...?width=...&cc=...` URL shape (HMAC-signed), registered regardless of which processor is active.
- Local-disk file access for reading originals and writing derivative output (this POC's storage backend).
- DI registration surface for "select a processor" (e.g. `AddImageProcessing().UseSkiaSharp()` / `.UseImageFlow()` / `.UseImageSharp()`) — the one-line swap the whole abstraction exists to enable.

No processor-specific logic belongs here — only what's identical regardless of which library ends up doing the actual pixel work.

## Answer

Built out `src/Umbraco.Image.Processing.Core` (+ a new `Umbraco.Image.Processing.Core.Tests` xUnit project, both added to the `.sln`). Solution and Core project build clean (0 warnings); 64 unit tests pass covering the pure/testable logic (command parsing, crop math, HMAC round-trips, header parsing, URL generation).

**Key design decision**: Core computes the crop pixel-rectangle and resolves EXIF orientation itself, *before* calling the processor — mirroring the split the map's Notes already called for on the ImageFlow side, generalized to both processors. To do that without depending on any imaging library (keeping Core genuinely processor-agnostic), `Media/ImageHeaderReader.cs` is a small from-scratch container-header parser (JPEG/PNG/GIF/BMP/WebP dimensions; JPEG-only EXIF orientation tag read) — no pixel decode, just the handful of header bytes each format needs. `IImageProcessor.ProcessAsync` therefore receives a `ResolvedImageCommand` with an already-computed pixel `CropRectangle?` and a resolved `ExifOrientation` ushort (1–8, matching the standard EXIF/TIFF values) — a processor just decodes, applies, and encodes; no focal-point or orientation math of its own.

**Ported math**: `Processing/ExifOrientationTransform.cs` and `Processing/ImageCropCalculator.cs` are direct ports of `SixLabors.ImageSharp.Web.ExifOrientationUtilities.Transform` (fetched from github.com/SixLabors/ImageSharp.Web, src/ImageSharp.Web/ExifOrientationUtilities.cs) and Umbraco's own `CropWebProcessor` crop-rectangle math (src/Umbraco.Cms.Imaging.ImageSharp/ImageProcessors/CropWebProcessor.cs in the sibling Umbraco-CMS repo) — same semantics as the research ticket already established for ImageFlow, now shared by both processors via Core instead of duplicated.

**Command surface implemented**: `width`, `height`, `format`, `quality`, `bgcolor` (hex only — `#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`), `autoorient` (bool, default true), `cc` (Umbraco's crop/focal-point command). Width/height clamping mirrors the plan doc's `OnParseCommandsAsync` (values ≤0 or ≥ configured max are dropped, not rejected).

**HMAC**: `Security/HmacSigner.cs` is Core's own scheme (HMAC-SHA256 over path + sorted non-`hmac` query pairs, hex-encoded) — not a port of SixLabors' `HMACUtilities`, since Core doesn't depend on ImageSharp.Web at all; signing (`ImageProcessingUrlGenerator`) and verification (the middleware) both build the same canonical string so they always agree.

**`IImageUrlGenerator`/`IImageDimensionExtractor`**: Core takes a direct `Umbraco.Cms.Core` (v18.1.1) package reference and implements Umbraco's own interfaces directly (`UrlGeneration/ImageProcessingUrlGenerator.cs`, `UrlGeneration/ImageProcessingDimensionExtractor.cs`) — genuine drop-in, not a lookalike. Registered unconditionally by `AddImageProcessing()`, regardless of which processor is later selected.

**DI surface**: `services.AddImageProcessing(configure)` (`DependencyInjection/ServiceCollectionExtensions.cs`) registers options, HMAC signer, local-disk original-source and derivative-cache, and the URL generator/dimension extractor; returns `IImageProcessingBuilder` for a processor package to extend with its own `UseSkiaSharp()`/`UseImageFlow()`/`UseImageSharp()` (`builder.Services.AddSingleton<IImageProcessor, ...>()`) — ticket's one-line-swap requirement.

**Middleware**: `Middleware/ImageProcessingMiddleware.cs` + `app.UseImageProcessing()`. `ImageProcessingOptions.RoutePrefix` (default `/media`) drives in-process vs. standalone: set it to `""` for standalone, where the whole app is the image service. Handles HMAC validation, pass-through for requests with no processing commands, local-disk derivative caching keyed by a SHA-256 hash of path+querystring, and correct `Content-Type`/`Cache-Control` response headers.

**Storage**: `Storage/LocalDiskOriginalImageSource.cs` (with path-traversal guarding) and `Storage/LocalDiskDerivativeImageCache.cs` — the POC's only implementation, per the map's standing local-disk-only decision.

**Facts for downstream tickets**: The SkiaSharp processor (SkiaSharp Processor) and ImageFlow processor (ImageFlow Processor) tickets both implement `Umbraco.Image.Processing.Core.Processing.IImageProcessor` and register via `IImageProcessingBuilder`; neither needs its own crop/orientation math — `ResolvedImageCommand.Crop`/`ExifOrientation` arrive pre-computed. `ImageHeaderReader`/`ImageCropCalculator`/`ExifOrientationTransform` are public, so both processor projects (or their tests) can reuse them directly if useful.
