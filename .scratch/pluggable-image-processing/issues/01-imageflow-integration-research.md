Type: research
Status: resolved

## Question

How should the ImageFlow processor project integrate with `Imageflow.NET` to hit the drop-in requirement (full ImageSharp.Web stock command surface + Umbraco's `cc` crop/focal-point command, served through Core's own middleware)?

Specifically:

- Does `Imageflow.NET` (the core `imageflow-dotnet` library) expose an in-process job API that can be driven directly from Core's `IImageProcessor` interface — decode bytes, apply a job graph equivalent to resize/crop/format/quality/bgcolor/autoorient, encode bytes — without needing `Imageflow.Server`'s own ASP.NET Core middleware or query-string parsing?
- Does `Imageflow.NET`'s job graph support an operation equivalent to Umbraco's crop/focal-point math (`CropWebProcessor`'s coordinate-based crop), or does that need to be computed in the processor project before handing pixel-space crop coordinates to ImageFlow?
- Is there any reason `Imageflow.Server`'s middleware would need to run at all given Core already owns command parsing, HMAC signing, and response writing — or does it conflict (e.g. double registration of endpoints, incompatible command vocabulary)?
- What NuGet package(s) are actually needed (`Imageflow.NET`, `Imageflow.NativeRuntime.*`), and are there native-runtime/platform caveats worth flagging in the quickstart docs?

Resolve via a `/research` subagent against ImageFlow's official docs/repo. Record the recommended integration approach as the answer — this unblocks the ImageFlow processor build ticket.

## Answer

Full findings, code samples, and citations: [`research/imageflow-integration.md`](../research/imageflow-integration.md).

**Recommended integration**: `IImageProcessor.Process()` in the ImageFlow processor calls `Imageflow.NET`'s in-process fluent job API directly — `ImageJob().Decode(bytes)` → `BuildNode` chain → `.Finish().InProcessAsync()` — bypassing `Imageflow.Server` entirely. No ASP.NET Core middleware or query-string parsing from Imageflow is involved anywhere.

- **In-process job API**: Yes, fully in-process (`ImageJob`/`BuildNode`, `InProcessAsync()`). Covers resize (`ConstrainWithin`/`Constrain`), crop (`Crop`/`Region`), bgcolor (`Constraint.CanvasColor`/`Region` background), format/quality (typed `IEncoderPreset` per format). **Autoorient has no dedicated node** — the caller must read the EXIF orientation tag and issue explicit `Rotate*`/`Flip*` calls itself.
- **Crop/focal-point math**: Imageflow has **no focal-point-aware crop node** — `Crop`/`Region` take plain pixel/percent rectangles only. The pixel-space rectangle for Umbraco's `cc` command must be computed by Core, using the same normalized-edge-distance math Umbraco's own `ImageSharp2` `CropWebProcessor` already implements (ported directly, not novel work).
- **Should `Imageflow.Server` run?**: No. It's self-contained middleware with an incompatible ImageResizer-flavored query vocabulary, its own disk cache, and its own license-watermark enforcement — all conflicting with Core's already-decided ownership of parsing/signing/response-writing. The library's own README directs non-HTTP-server use cases to `Imageflow.NET` directly.
- **NuGet/native runtime**: Single package `Imageflow.AllPlatforms` (net8.0+) bundles `Imageflow.Net` + native runtimes for all RIDs. Native packages are still RC-versioned (`2.3.1-rc01`). Quickstart caveats: Windows hosts may need the VC++ redistributable; container builds should target a single RID explicitly rather than the "all platforms" bundle; **licensing is the biggest caveat** — actually executing a job (`InProcessAsync()`) requires either AGPLv3 compliance or a commercial imazen license, regardless of whether `Imageflow.Server` is used. This is independent of the integration-approach choice and should be stated plainly in the quickstart doc.
