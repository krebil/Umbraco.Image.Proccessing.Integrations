Type: wayfinder:map

## Destination

A working proof-of-concept for a pluggable Umbraco image-processing abstraction. A shared Core project defines the processor abstraction, models, and shared concerns (command parsing/normalization, HMAC signing) independent of any specific imaging library. Two processor projects — SkiaSharp and ImageFlow — implement that abstraction as drop-in replacements for `Umbraco.Cms.Imaging.ImageSharp`, each supporting the full ImageSharp.Web stock query-string command surface (`width`, `height`, `format`, `quality`, `bgcolor`, autoorient) plus Umbraco's `cc` crop/focal-point command, with HMAC signing working identically across all three. Each processor must be runnable both **in-process** (same app service as Umbraco) and as a **separate standalone deployment**, with an individual quickstart `.md` guide per mode, written so they can be linked directly from blog posts or the README.

Proven by manual click-through: the Aspire AppHost boots, the sample site renders resized/cropped images correctly under each processor config, the `cc` crop command produces the correct crop, and HMAC-signed URLs are accepted/rejected correctly. No automated parity suite required for the POC.

## Notes

- **This map carries execution.** Tickets are build tasks resolved by implementing them directly, not pure decisions — an intentional override of wayfinder's default (see the map skill's "Plan, don't do" section). Still: never resolve more than one ticket per session, research tickets excepted.
- Prior art: `imagesharp-standalone-service-plan.md` at the repo root — the standalone-deployment redirect/CDN pattern it describes carries over, made processor-agnostic.
- Repo already pins Umbraco.Cms 18.1.1 (`src/Umbraco/Directory.Packages.props`) and targets net10.0 across projects.
- Existing projects: `src/Umbraco` (sample site, currently unconfigured for imaging) and `src/Umbraco.Image.Processing.Core` (empty scaffold).
- Standing preferences locked during grilling:
  - Storage backend for the POC is **local disk only**. Azure Blob support is real future work but out of scope here (see below).
  - Core owns the middleware, canonical command parsing/normalization/validation, and HMAC signing. Each processor project implements only `IImageProcessor` (decode → transform → encode).
  - Core owns **one single** `IImageUrlGenerator` + dimension-extractor implementation, shared by all three processors (output format never varies by processor, so no per-processor variant is needed).
  - Standalone mode needs no URL-generation code — it's documentation only (the redirect/CDN pattern).
  - Quickstart docs: **two total** (in-process, standalone), each generic across processors via a config swap — not six.
  - `.NET Aspire` (`Umbraco.Image.Processing.AppHost`) orchestrates local dev/demo only — wires up the sample site, the standalone service, and later storage emulators. The quickstart docs stay Aspire-free.
  - In-process vs standalone switch in the sample `Umbraco` project is a config value (e.g. `ImageProcessing:Mode = InProcess | Standalone`).
  - ImageFlow's integration mechanism (`Imageflow.NET` direct vs. anything from `Imageflow.Server`) is left to agent discretion — resolve via the research ticket, prioritizing drop-in query-string parity over reusing existing middleware.
  - **Imageflow licensing**: actually executing a job via `Imageflow.NET` (`InProcessAsync()`) requires AGPLv3 compliance or a commercial imazen license — independent of using `Imageflow.Server` or not. Flag this plainly in the quickstart docs (tickets 08/09).
- Relevant skills: `/tdd` for the Core/processor build tickets if red-green fits naturally; `/research` for the ImageFlow ticket.

## Decisions so far

- [ImageFlow Integration Research](issues/01-imageflow-integration-research.md) — Call `Imageflow.NET`'s in-process fluent job API (`ImageJob`/`BuildNode` → `InProcessAsync()`) directly from `IImageProcessor.Process()`, bypassing `Imageflow.Server` entirely. Core computes the `cc` crop pixel-rectangle and EXIF-orientation rotation (ported from Umbraco's own `CropWebProcessor` math) since Imageflow has no focal-point or autoorient node. Single package `Imageflow.AllPlatforms`; native runtimes still RC-versioned. Full detail: [`research/imageflow-integration.md`](research/imageflow-integration.md).
- [Core Abstraction](issues/02-core-abstraction.md) — Built `src/Umbraco.Image.Processing.Core`: canonical command model, `IImageProcessor` seam, HMAC signing, the shared `IImageUrlGenerator`/`IImageDimensionExtractor` (implementing Umbraco's own interfaces directly), local-disk storage, and `AddImageProcessing()` DI surface. Core resolves the crop rectangle and EXIF orientation itself — via a small from-scratch header/EXIF reader, no imaging-library dependency — so processors only decode/transform/encode. 64 unit tests, solution builds clean.
- [SkiaSharp Processor](issues/03-skiasharp-processor.md) — Built `src/Umbraco.Image.Processing.SkiaSharp`: decode → crop → orientation-correct (exact per-pixel matrices derived from Core's own `ExifOrientationTransform`) → resize → background-flatten → encode, plus `UseSkiaSharp()` DI registration. SkiaSharp's native encoder only supports `jpg`/`jpeg`/`png`/`webp` — `gif`/`bmp` requests throw `NotSupportedException`, a real gap the sample/quickstart tickets (05/06/08/09) need to account for. 16 unit tests, solution builds clean.

## Not yet specified

- None currently — the grilling session covered the POC's scope in enough depth that every in-scope area is already ticketed below. Revisit if a ticket's resolution surfaces something unticketable.

## Out of scope

- **Azure Blob storage support** — real future work ("both should be supported in the end" per the user), but not required to prove the abstraction; local disk only for this POC.
- **Production hardening** — test coverage bar, CI/CD, NuGet packaging/publishing, semver strategy across Core/SkiaSharp/ImageFlow packages. Follow-on effort via `/grill-with-docs` once the POC proves the abstraction, per the main flow (not another wayfinder map — this becomes well-scoped once real code exists).
- **A third processor** beyond SkiaSharp and ImageFlow.
- **Automated parity/output test suite** — manual verification is the POC's bar; an automated suite belongs to the production-hardening follow-on.
