Type: task
Status: open
Blocked by: 02

## Question

Create `src/Umbraco.Image.Processing.SkiaSharp`, implementing Core's `IImageProcessor` against the raw `SkiaSharp` NuGet package (no existing "SkiaSharp.Web" middleware exists, so this is built from scratch):

- Decode → resize/crop/format/quality/bgcolor/autoorient → encode, covering the full locked command surface.
- Umbraco's `cc` crop/focal-point command: reproduce the coordinate-based crop math (equivalent to `Umbraco.Cms.Imaging.ImageSharp`'s `CropWebProcessor`) against `SkiaSharp`'s canvas/bitmap APIs.
- Register as a selectable processor via Core's DI surface (`.UseSkiaSharp()`).

This is the reference/first processor implementation — it's what proves Core's `IImageProcessor` seam is shaped correctly before the (harder) ImageFlow processor is attempted.
