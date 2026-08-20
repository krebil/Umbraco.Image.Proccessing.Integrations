Type: task
Status: open
Blocked by: 02, 03

## Question

Wire up the existing `src/Umbraco` project as the in-process demo:

- Reference Core and (at minimum) the SkiaSharp processor project; register the abstraction via `AddImageProcessing().UseSkiaSharp()` (or equivalent) in `Program.cs`.
- Add the `ImageProcessing:Mode = InProcess | Standalone` config switch: `InProcess` registers the processor pipeline directly into this app's own middleware; `Standalone` instead wires the redirect-to-external-service middleware (see the standalone service ticket) — same shape as the plan doc's `/media` redirect, made processor-agnostic.
- Configure local-disk media storage and confirm the sample renders resized/cropped images (including a Cropper-configured `cc` URL) correctly with the SkiaSharp processor selected.
- Once the ImageFlow processor ticket lands, confirm the same sample also works with `.UseImageFlow()` selected — no code changes beyond the DI registration line.
