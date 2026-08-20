Type: research-notes

# Imageflow integration research

Resolves the research question in `.scratch/pluggable-image-processing/issues/01-imageflow-integration-research.md`. All claims below are sourced from primary/official Imageflow material: the `imazen/imageflow-dotnet` GitHub repo (README + source), `imazen/imageflow-dotnet-server` GitHub repo (README), `docs.imageflow.io`, and official NuGet package listings. No blog posts or Stack Overflow used.

## 1. In-process job API

**Yes.** `Imageflow.NET` (package `Imageflow.Net` / `Imageflow.AllPlatforms`, namespace `Imageflow.Fluent`) exposes a fully in-process job-graph API — `ImageJob` + a fluent `BuildNode` chain — that decodes bytes, builds a graph, and encodes bytes with no ASP.NET Core, no HTTP pipeline, and no query-string parsing involved. Execution is via `.Finish().InProcessAsync()`.

Source: [imazen/imageflow-dotnet README](https://github.com/imazen/imageflow-dotnet/blob/main/README.md), "Edit images with the fluent API" example:

```csharp
using Imageflow.Fluent;
public async Task TestAllJob()
{
    var imageBytes = Convert.FromBase64String("...");
    using (var b = new ImageJob())
    {
        var r = await b.Decode(imageBytes)
            .FlipVertical()
            .Rotate90()
            .CropWhitespace(80, 0.5f)
            .Distort(30, 20)
            .Crop(0,0,10,10)
            .Region(-5,-5,10,10, AnyColor.Black)
            .RegionPercent(-10f, -10f, 110f, 110f, AnyColor.Transparent)
            .BrightnessSrgb(-1f)
            .ExpandCanvas(5,5,5,5, AnyColor.FromHexSrgb("FFEECCFF"))
            .ResizerCommands("width=10&height=10&mode=crop")
            .ConstrainWithin(5, 5)
            .Watermark(new BytesSource(imageBytes), new WatermarkOptions() /* ... */)
            .EncodeToBytes(new MozJpegEncoder(80, true))
            .Finish().InProcessAsync();
    }
}
```

There is also a JSON-job-graph and a command-string entry point on the same in-process executor (`ImageJob.BuildCommandString(...)`), still with no HTTP dependency:

```csharp
var r = await b.BuildCommandString(
    new MemorySource(imageBytes),
    new BytesDestination(),
    "width=3&height=2&mode=stretch&scale=both&format=webp&webp.quality=80")
    .Finish().InProcessAsync();
```

Coverage of the requested command set, per README example and `BuildNode.cs`/`Constraint.cs` source (`src/Imageflow/Fluent/BuildNode.cs`, `Constraint.cs` in that repo):
- **Resize**: `ConstrainWithin(w,h)`, `Constrain(new Constraint(mode, w, h))` with modes `distort`/`within`/`fit`/`fit_crop`/`fit_pad`/etc. — [docs.imageflow.io/json/constrain.html](https://docs.imageflow.io/json/constrain.html).
- **Crop**: `Crop(x1,y1,x2,y2)` and `Region(x1,y1,x2,y2,bgColor)` / `RegionPercent(...)` — pixel or percent rectangles (see Q2).
- **Format/Quality**: not a generic `format=`/`quality=` node — you pick a typed `IEncoderPreset` (`MozJpegEncoder(quality, progressive)`, `WebPLossyEncoder(quality)`, `WebPLosslessEncoder()`, `LibPngEncoder`/`LodePngEncoder`, `GifEncoder`) and pass it to `EncodeToBytes(preset)`.
- **Bgcolor**: `Constraint.CanvasColor` / `Region`'s `background_color` parameter — used for padding, matches Umbraco's `bgcolor` intent.
- **Autoorient**: **no dedicated node exists.** `DecodeCommands` (passed to `Decode(source, ioId, commands)`) only exposes JPEG/WebP downscale hints and color-profile handling — [`src/Imageflow/Fluent/DecodeCommands.cs`](https://github.com/imazen/imageflow-dotnet/blob/main/src/Imageflow/Fluent/DecodeCommands.cs). Orientation is exposed only as manual `FlipVertical()`/`FlipHorizontal()`/`Rotate90()`/`Rotate180()`/`Rotate270()`/`Transpose()` nodes — [docs.imageflow.io/json/rotate_flip.html](https://docs.imageflow.io/json/rotate_flip.html) confirms these are the only rotate/flip primitives, with no orientation-metadata-aware node. **The caller (Core or the processor) must read the EXIF orientation tag itself and issue the matching rotate/flip node(s)** — this mirrors what Umbraco's own `ImageSharp` `CropWebProcessor` already does today (see Q2).

## 2. Crop/focal-point math

**Imageflow has no focal-point-aware crop node.** Its crop primitives take an explicit axis-aligned rectangle, not a focal point + target-size computation:

- `Crop(int x1, int y1, int x2, int y2)` — "Crops the image to the given coordinates." Pixel coordinates, source: [`BuildNode.cs`](https://github.com/imazen/imageflow-dotnet/blob/main/src/Imageflow/Fluent/BuildNode.cs) (XML doc + signature at line ~154).
- `Region(int x1, int y1, int x2, int y2, AnyColor backgroundColor)` — "Region is like a crop command, but you can specify coordinates outside of the image and thereby add padding. It's like a window. Coordinates are in pixels." (same file, line ~176-186).
- `RegionPercent(float x1, float y1, float x2, float y2, AnyColor backgroundColor)` — same semantics, coordinates as percentages of image size (line ~210-221).
- Querystring-API equivalent (only relevant if going through `BuildCommandString`, not the fluent graph): `crop=x1,y1,x2,y2` with an optional `cropxunits=100&cropyunits=100` to make those percentages instead of pixels — [docs.imageflow.io/querystring/transforms.html](https://docs.imageflow.io/querystring/transforms.html).
- The nearest thing to "focal point" is `ConstrainGravity` (`{x: 0..100, y: 0..100}` percentage anchor) used by `Constrain()`'s auto-crop-to-aspect modes (`fit_crop`, `aspect_crop`, `within_crop`) — "determines how the image is anchored when cropped or padded," default center — [docs.imageflow.io/json/constrain.html](https://docs.imageflow.io/json/constrain.html), [`ConstraintGravity.cs`](https://github.com/imazen/imageflow-dotnet/blob/main/src/Imageflow/Fluent/ConstraintGravity.cs). This is Imageflow computing its *own* auto-crop from an anchor point and a target aspect ratio — it does not accept Umbraco's `cc` payload (four independent edge-distance fractions) as input, and it isn't a drop-in match for `CropWebProcessor`'s semantics.

**Conclusion: the pixel-space crop rectangle must be computed by Core/the processor before calling Imageflow.** This is not new work — it is exactly the algorithm Umbraco's own `Umbraco.Cms.Imaging.ImageSharp2`'s `CropWebProcessor` already implements (read directly from the sibling `Umbraco-CMS` repo, `src/Umbraco.Cms.Imaging.ImageSharp2/ImageProcessors/CropWebProcessor.cs`):

```csharp
// cc=left,top,right,bottom as normalized (0..1) distances from each edge
var left = Math.Clamp(coordinates[0], 0, 1);
var top = Math.Clamp(coordinates[1], 0, 1);
var right = Math.Clamp(1 - coordinates[2], 0, 1);
var bottom = Math.Clamp(1 - coordinates[3], 0, 1);
var orientation = GetExifOrientation(image, commands, parser, culture);
Vector2 xy1 = ExifOrientationUtilities.Transform(new Vector2(left, top), Vector2.Zero, Vector2.One, orientation);
Vector2 xy2 = ExifOrientationUtilities.Transform(new Vector2(right, bottom), Vector2.Zero, Vector2.One, orientation);
Size size = image.Image.Size();
return Rectangle.Round(RectangleF.FromLTRB(
    MathF.Min(xy1.X, xy2.X) * size.Width, MathF.Min(xy1.Y, xy2.Y) * size.Height,
    MathF.Max(xy1.X, xy2.X) * size.Width, MathF.Max(xy1.Y, xy2.Y) * size.Height));
```

Because Core owns command parsing/normalization (per `map.md`), this exact normalized→pixel-rect math belongs in Core, producing a plain `x1,y1,x2,y2` pixel rectangle. Each processor (SkiaSharp, ImageFlow) just applies it: the ImageFlow processor calls `BuildNode.Crop(x1, y1, x2, y2)` (or `Region` if padding beyond image bounds is ever needed) with those already-computed pixel coordinates. No Imageflow-side focal-point logic is usable or needed.

## 3. Does Imageflow.Server middleware need to run at all?

**No — and running it would conflict with Core's architecture.** `Imageflow.Server` is self-contained ASP.NET Core middleware that owns its own HTTP pipeline stage, its own query-string vocabulary, and its own caching/licensing:

- It registers itself via `app.UseImageflow(new ImageflowMiddlewareOptions()...)`, ahead of `app.UseEndpoints(...)`, and intercepts matching requests before your own endpoints/middleware see them — [imazen/imageflow-dotnet-server README, "Basic Installation"](https://github.com/imazen/imageflow-dotnet-server/blob/main/README.md).
- Its query-string API is "Imageflow Querystring API (compatible with ImageResizer)" — [same README, "Features"](https://github.com/imazen/imageflow-dotnet-server/blob/main/README.md) — an ImageResizer-derived vocabulary (`srotate`, `sflip`, `cropxunits`, etc.), not ImageSharp.Web's or Umbraco's `cc` vocabulary. Running it alongside Core's own parser means two different command-string dialects competing for the same query string, or Core having to translate its canonical model back into ImageResizer syntax pointlessly.
- It ships its own on-disk cache ("Size-constrained Disk Caching with a write-ahead-log") and its own path-mapping/remote-source system (S3, Azure Blob, custom blob services) — all things Core already owns per the map's standing decisions (Core owns middleware + HMAC signing + response writing; storage backend is local disk for the POC).
- It has its own license-enforcement mechanism baked into the middleware: without a paid license key, `SetLicenseKey(EnforceLicenseWith.RedDotWatermark, ...)` is the documented fallback, i.e. **unlicensed `Imageflow.Server` usage watermarks output images with a red dot** — [same README, step 7](https://github.com/imazen/imageflow-dotnet-server/blob/main/README.md). This enforcement is specific to the `Imageflow.Server` middleware package, separate from the underlying `Imageflow.NET`/native library licensing (see Q4 caveat below).
- The README itself directs users away from it for this exact scenario: **"If you don't need an HTTP server, [try Imageflow.NET](https://github.com/imazen/imageflow-dotnet)."** — [imazen/imageflow-dotnet-server README, opening paragraph](https://github.com/imazen/imageflow-dotnet-server/blob/main/README.md).

Since Core already owns command parsing, HMAC signing, and response writing, running `Imageflow.Server` would mean double endpoint/middleware registration, a second incompatible command vocabulary, a second (redundant) disk cache, and double image processing risk if both pipelines ever touch the same request. **`Imageflow.Server` should not be used at all in this architecture.**

## 4. NuGet packages and native runtime caveats

**Required package (current, .NET 8+):**
```
dotnet add package Imageflow.AllPlatforms
```
`Imageflow.AllPlatforms` depends on `Imageflow.Net` (the managed API, ≥0.15.1) and `Imageflow.NativeRuntime.All` (≥2.3.1-rc01), which in turn bundles the native binaries for every supported RID — source: [NuGet — Imageflow.AllPlatforms](https://www.nuget.org/packages/Imageflow.AllPlatforms), [NuGet — Imageflow.Net](https://www.nuget.org/packages/Imageflow.NET), [imazen/imageflow-dotnet README](https://github.com/imazen/imageflow-dotnet/blob/main/README.md).

**Supported RIDs** (per README and the individual native-runtime package listing on NuGet — [nuget.org/packages?q=Imageflow+AND+NativeRuntime](https://www.nuget.org/packages?q=Imageflow+AND+NativeRuntime)): `win-x86`, `win-x86_64`(win-x64), `win-arm64`, `linux-x64` (`ubuntu-x86_64`), `linux-arm64`, `osx-x86_64` (osx-x64), `osx-arm64`. All are still versioned as **`2.3.1-rc01`** (pre-1.0/release-candidate), consistent with a maintainer's own comment that the project "is still active" but "not enough people are using it for it to exit release candidate stage" — [imazen/imageflow-dotnet issue #6 comment](https://github.com/imazen/imageflow-dotnet/issues/6).

**Caveats to flag in the quickstart:**
1. **`Any CPU` + `PackageReference` on .NET Framework 4.x needs an explicit `<RuntimeIdentifiers>`** in the `.csproj`, or the build fails with `Your project file doesn't list 'win' as a "RuntimeIdentifier"` — not relevant to this POC (net10.0-only per `map.md`) but worth a one-line note if the Core/processor projects are ever multi-targeted down to net48. Source: [imazen/imageflow-dotnet README](https://github.com/imazen/imageflow-dotnet/blob/main/README.md).
2. **Native binaries are not copied transitively for packages.config-based projects** — irrelevant for SDK-style/PackageReference projects (this repo), but flag if any consuming host project is still packages.config-based.
3. **Windows hosts may be missing the VC++ runtime** the native `imageflow.dll` needs — README explicitly links the 32-bit and 64-bit redistributable installers as a fix. Worth a line in a Windows-hosted quickstart.
4. **AOT/Trimming**: the library switched from Newtonsoft.Json to `System.Text.Json` specifically "to support AOT and trimming" ([CHANGES.md](https://github.com/imazen/imageflow-dotnet/blob/main/CHANGES.md), referenced from the README) — but the underlying processing is done by a native (Rust) library invoked via P/Invoke, so container/self-contained-publish scenarios still need the correct native asset for the target RID to be present in the publish output; a linux-container Dockerfile should target a single RID (e.g. `linux-x64`) explicitly rather than relying on `Imageflow.AllPlatforms`'s "all RIDs" bundle, to avoid shipping every platform's native binary in the image.
5. **Licensing is the single biggest quickstart caveat, and it is independent of the Imageflow.Server-vs-Imageflow.NET choice.** Per the README's License section: *"Imageflow is dual licensed under a commercial license and the AGPLv3. Imageflow.NET is tri-licensed under a commercial license, the AGPLv3, and the Apache 2 license... Imageflow.NET's Apache 2 license allows for integration with non-copyleft products, as long as jobs are not actually executed (since the AGPLv3/commercial license is needed when libimageflow is linked at runtime)."* — [imazen/imageflow-dotnet README, "License"](https://github.com/imazen/imageflow-dotnet/blob/main/README.md). In other words: merely referencing `Imageflow.NET` is Apache-2-covered, but **actually calling `.InProcessAsync()` to run a job** (exactly what `IImageProcessor.Process()` would do) requires either AGPLv3 compliance (source availability obligations for the whole network-served application, per AGPL §13) or a commercial license from imazen. This applies identically whether the integration goes through `Imageflow.Server` or `Imageflow.NET` directly — the quickstart should say so plainly rather than implying that skipping `Imageflow.Server` avoids the license question.

## Recommendation

**The ImageFlow processor project's `IImageProcessor.Process()` should call `Imageflow.NET`'s in-process job API directly (`ImageJob` + `BuildNode` fluent graph, executed via `.Finish().InProcessAsync()`), bypassing `Imageflow.Server` entirely.** Decode via `ImageJob.Decode(byte[])`, apply `ConstrainWithin`/`Constrain` for resize, `Crop`/`Region` for the `cc` command using a pixel rectangle computed by Core with the same normalized-coordinate math Umbraco's `CropWebProcessor` already uses, explicit `FlipHorizontal`/`Rotate*` calls driven by an EXIF-orientation read for `autoorient`, `Constraint.CanvasColor`/`Region`'s background color for `bgcolor`, and a typed `IEncoderPreset` (`MozJpegEncoder`, `WebPLossyEncoder`, etc.) chosen from the requested `format`/`quality` for encode.

This is the only approach that satisfies the ticket's stated priority ("drop-in query-string parity over reusing existing middleware"): `Imageflow.Server` brings its own incompatible ImageResizer-flavored query-string dialect, its own middleware registration point, its own disk cache, and its own license-watermark enforcement — all of which either duplicate or actively fight Core's already-decided ownership of parsing, HMAC signing, and response writing (Q3). The in-process job API is fully capable of the full requested command surface (Q1) with the one gap — focal-point crop math and EXIF-orientation-aware rotation — needing to be computed by Core/the processor rather than delegated to Imageflow, which is not a blocker since that exact math already exists as a proven reference implementation in Umbraco's own `ImageSharp2` processor (Q2). The only NuGet footprint needed is `Imageflow.AllPlatforms` (net8.0+), with the RC-quality native-runtime versioning and the AGPLv3-or-commercial licensing obligation flagged explicitly in the quickstart doc as adoption risks independent of this integration choice (Q4).
