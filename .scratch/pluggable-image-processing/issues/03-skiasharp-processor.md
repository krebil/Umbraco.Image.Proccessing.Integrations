Type: task
Status: resolved
Blocked by: 02

## Question

Create `src/Umbraco.Image.Processing.SkiaSharp`, implementing Core's `IImageProcessor` against the raw `SkiaSharp` NuGet package (no existing "SkiaSharp.Web" middleware exists, so this is built from scratch):

- Decode → resize/crop/format/quality/bgcolor/autoorient → encode, covering the full locked command surface.
- Umbraco's `cc` crop/focal-point command: reproduce the coordinate-based crop math (equivalent to `Umbraco.Cms.Imaging.ImageSharp`'s `CropWebProcessor`) against `SkiaSharp`'s canvas/bitmap APIs.
- Register as a selectable processor via Core's DI surface (`.UseSkiaSharp()`).

This is the reference/first processor implementation — it's what proves Core's `IImageProcessor` seam is shaped correctly before the (harder) ImageFlow processor is attempted.

## Answer

Built `src/Umbraco.Image.Processing.SkiaSharp` (+ a matching `.Tests` project, both added to the `.sln` under a new solution folder mirroring Core's layout). Solution builds clean (0 warnings); 16 unit tests pass.

**`SkiaSharpImageProcessor`** implements `IImageProcessor.ProcessAsync` as a straight pipeline: decode (raw `SKBitmap.Decode`, no auto-orient) → crop → orientation-correct → resize → background-flatten → encode. Each step is skipped when Core already resolved it away (no crop, `TopLeft`/`Unknown` orientation, no size change, no bgcolor), and only steps that actually allocate a new `SKBitmap` are tracked for disposal — avoids double-disposing the original decode.

**Crop**: `SKBitmap.ExtractSubset` against `command.Crop`, clamped defensively to source bounds.

**Orientation**: applied *after* crop, since `ImageCropCalculator`'s rectangle is in stored (pre-orientation) pixel space per Core's design — confirmed by re-deriving `ExifOrientationTransform.Transform`'s eight cases as pixel-space matrices rather than composing canvas rotate/flip calls by hand (the four transpose orientations are easy to get subtly wrong that way). Verified each matrix by hand-tracing a 2x2 four-corner test image through the transform before writing the corresponding assertions — tests lock in `RightTop` (90° CW), `LeftTop` (transpose), and `BottomRight` (180°) explicitly.

**Resize**: exact `width`×`height` when both given; aspect-preserving when only one is given (matches `ResizeWebProcessor`-style behavior for a crop-then-resize pair sharing one aspect ratio).

**Background**: composites onto a solid `SKColor` whenever `BackgroundColor` is set, regardless of target format — a flatten, not a format-conditional fallback.

**Format**: SkiaSharp's native encoder only supports `jpg`/`jpeg`/`png`/`webp` — `gif` and `bmp` are not encodable by SkiaSharp regardless of source format. `SupportedOutputFormats` reports the four supported formats; `Encode` throws `NotSupportedException` for anything else. This is a genuine capability gap, not an oversight: **a `gif`/`bmp` original run through this processor without an explicit `format=` override to `jpg`/`png`/`webp` will throw**, since `Format` defaults to the source's own format. Flagging for the in-process/standalone sample tickets (05/06) and quickstarts (08/09) — either avoid gif/bmp originals in the demo, or always pass `format=` for them.

**DI**: `UseSkiaSharp()` (`SkiaSharpImageProcessingBuilderExtensions`) registers `SkiaSharpImageProcessor` as `IImageProcessor` — the one-line swap.

**Package**: `SkiaSharp` 4.151.1 + `SkiaSharp.NativeAssets.Linux` 4.151.1 (explicit `Version=` attrs, matching Core's non-CPM convention — the repo's `Directory.Packages.props` is scoped to `src/Umbraco` only).
