Type: task
Status: resolved
Blocked by: 01, 02

## Question

Create `src/Umbraco.Image.Processing.ImageFlow`, implementing Core's `IImageProcessor` using the integration approach the ImageFlow research ticket recommends:

- Decode → resize/crop/format/quality/bgcolor/autoorient → encode, covering the full locked command surface, driven through Core's middleware (not `Imageflow.Server`'s, unless the research ticket concluded otherwise).
- Umbraco's `cc` crop/focal-point command, translated into whatever ImageFlow's job-graph API needs.
- Register as a selectable processor via Core's DI surface (`.UseImageFlow()`).

Apply the research ticket's answer directly — don't re-litigate the integration-mechanism decision here, just build against it.

## Answer

Built `src/Umbraco.Image.Processing.ImageFlow`: `ImageFlowImageProcessor` calls `Imageflow.NET`'s in-process fluent job API directly (`ImageJob().Decode(bytes)` → `BuildNode` chain → `.Finish().WithCancellationToken(...).InProcessAsync()`), never touching `Imageflow.Server`, exactly per the research ticket. `UseImageFlow()` registers it via Core's DI surface. Package: `Imageflow.AllPlatforms` 0.15.1.

- **Crop**: `command.Crop` (Core's pixel rectangle) is clamped against the source's actual dimensions — read via Core's own `ImageHeaderReader` on the buffered source bytes, so no imaging-library dependency is added just to know the pre-decode size — then applied via `BuildNode.Crop(x1, y1, x2, y2)`.
- **Autoorient**: composed from Imageflow's discrete `FlipHorizontal`/`FlipVertical`/`Rotate90`/`Rotate180`/`Rotate270`/`Transpose` nodes, one call per orientation. **Verified empirically against a known four-corner test image** (not just derived from docs): Imageflow's `Rotate90()`/`Rotate270()` turned out to name their direction opposite to what `ExifOrientationTransform`'s `RightTop`/`LeftBottom` cases imply, so those two are swapped relative to the naive mapping. All 8 orientations now covered by passing pixel-exact tests.
- **Resize**: since Imageflow's graph is declarative (no intermediate pixel access mid-build), the target size is computed up front the same way `SkiaSharpImageProcessor` does (duplicated `ComputeTargetSize` helper — single-dimension aspect-preserving math), then applied via `Constrain(new Constraint(ConstraintMode.Distort, w, h))` for an exact stretch.
- **Bgcolor — the one real surprise**: Imageflow has no dedicated "flatten transparency onto a color" node. `Region`/`Crop` copy pixel alpha unchanged (confirmed empirically — a same-bounds `Region` call was a complete no-op on a transparent pixel), and `Constrain`'s `canvas_color` is documented only for letterboxing added by padding. **Found empirically**: a pad-mode `Constrain` (`ConstraintMode.Within_Pad` + `SetCanvasColor(...)`) still composites the source over its canvas color even when the target size exactly matches the current size (no actual padding occurs) — that's what's used to flatten transparent pixels. This isn't documented anywhere; it was discovered by writing a failing test and trying alternatives until one passed. Flagged in code comments for whoever hits this next.
- **Formats**: `jpg`/`jpeg` → `MozJpegEncoder`, `png` → `LodePngEncoder` (not the obsolete `LibPngEncoder`), `webp` → `WebPLossyEncoder`, `gif` → `GifEncoder`. Unlike SkiaSharp, Imageflow **does** support `gif` output — but like SkiaSharp, neither has a `bmp` encoder, so `bmp` still throws `NotSupportedException` (an existing, accepted gap per ticket 03).

19 unit tests (SkiaSharp used only as an independent fixture-builder/decoder, never as the thing under test), solution builds clean, full solution test run green (99 tests total across Core/SkiaSharp/ImageFlow).

Note: this session ran against live NuGet/native binaries (network access was available), so the orientation and background-flatten findings above are empirically verified, not just researched — worth knowing since most sessions in this environment can't assume that.
