# 01 — Split UmbracoExtensions out of Core

**What to build:** Core stops carrying any Umbraco dependency. Move the Umbraco-specific `IImageUrlGenerator`/`IImageDimensionExtractor` implementations (and the Umbraco-only slice of the DI wiring) out of `Umbraco.Image.Processing.Core` into a new project, `Umbraco.Image.Processing.UmbracoExtensions`. After the move, `Umbraco.Image.Processing.Core.csproj` has no `Umbraco.Cms.Core` `PackageReference` at all; `UmbracoExtensions` carries that reference alone, pinned to an open floor (`>= 18.0.0`, no upper-major cap — ADR-0005) instead of the current exact pin. The in-process sample (`src/Umbraco`) references both Core and `UmbracoExtensions` and continues to build, boot, and serve resized/cropped images exactly as it does today.

**Blocked by:** None — can start immediately.

**Status:** resolved

- [x] New `Umbraco.Image.Processing.UmbracoExtensions` project exists, added to the solution, targeting `net10.0`
- [x] `ImageProcessingUrlGenerator` and `ImageProcessingDimensionExtractor` (and any other Umbraco-interface implementations) live in `UmbracoExtensions`, not Core
- [x] `Umbraco.Image.Processing.Core.csproj` has zero `Umbraco.Cms.Core` (or any `Umbraco.Cms.*`) `PackageReference`
- [x] `UmbracoExtensions.csproj`'s `Umbraco.Cms.Core` reference is an open floor (`>= 18.0.0`), no upper-major cap
- [x] Existing Core unit tests that exercised the moved Umbraco-interface code now live under a test project for `UmbracoExtensions` (or are otherwise relocated, not dropped)
- [x] `src/Umbraco` (in-process sample) references `UmbracoExtensions` and its DI registration is updated accordingly
- [x] Solution builds clean; in-process sample boots and serves pass-through/resize/crop/format requests correctly (manual `curl` check or existing automated coverage), matching pre-split behavior

## Comments

- New project `src/Umbraco.Image.Processing.UmbracoExtensions/` holds `UrlGeneration/ImageProcessingUrlGenerator.cs`, `UrlGeneration/ImageProcessingDimensionExtractor.cs`, and a new `DependencyInjection/UmbracoExtensionsImageProcessingBuilderExtensions.cs` exposing `AddUmbracoImageProcessing()` — mirrors the existing `UseSkiaSharp()`/`UseImageFlow()` builder-extension pattern, moving the `services.Replace(...)` calls for `IImageUrlGenerator`/`IImageDimensionExtractor` out of Core's `AddImageProcessing()`.
- `ImageCropCalculator.cs` was checked and left in Core — its only `Umbraco.Cms` mention is a doc comment citing prior art (`Umbraco.Cms.Imaging.ImageSharp.CropWebProcessor`), not an actual reference.
- `src/Umbraco/Program.cs` now chains `.AddUmbracoImageProcessing()` off `AddImageProcessing()` unconditionally (both `InProcess` and `Standalone` modes), since this app is always an Umbraco instance — mode only changes how image *requests* are routed, not whether Umbraco's own `IImageUrlGenerator`/`IImageDimensionExtractor` need overriding.
- `ImageProcessingUrlGeneratorTests.cs` moved to a new `Umbraco.Image.Processing.UmbracoExtensions.Tests` project (10 tests, all passing). Core.Tests still has 71 passing tests post-split; SkiaSharp (16) and ImageFlow (19) unrelated and unaffected.
- Verified `Umbraco.Cms.*` is absent from both Core's and the standalone Service's full dependency tree (direct + transitive) via `dotnet list package --include-transitive`.
- Manually verified end-to-end against the running in-process sample: demo page renders HMAC-signed URLs from the relocated `ImageProcessingUrlGenerator`; a signed resize request returns `200 image/jpeg`; a tampered signed request returns `400`; `/umbraco` backoffice boots (`200`).
