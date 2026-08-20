Type: task
Status: resolved
Blocked by: 02, 03

## Question

Wire up the existing `src/Umbraco` project as the in-process demo:

- Reference Core and (at minimum) the SkiaSharp processor project; register the abstraction via `AddImageProcessing().UseSkiaSharp()` (or equivalent) in `Program.cs`.
- Add the `ImageProcessing:Mode = InProcess | Standalone` config switch: `InProcess` registers the processor pipeline directly into this app's own middleware; `Standalone` instead wires the redirect-to-external-service middleware (see the standalone service ticket) — same shape as the plan doc's `/media` redirect, made processor-agnostic.
- Configure local-disk media storage and confirm the sample renders resized/cropped images (including a Cropper-configured `cc` URL) correctly with the SkiaSharp processor selected.
- Once the ImageFlow processor ticket lands, confirm the same sample also works with `.UseImageFlow()` selected — no code changes beyond the DI registration line.

## Answer

Wired `src/Umbraco` as the in-process demo:

- **`Umbraco.csproj`**: added `ProjectReference`s to Core, SkiaSharp, and ImageFlow.
- **`appsettings.json`**: added an `ImageProcessing` config section — `Mode` (`InProcess` default), `Processor` (`SkiaSharp` default), `RoutePrefix`, `OriginalsRootPath`, `DerivativeCacheRootPath`, and `Standalone:BaseUrl`. Also added the standard SQLite `ConnectionStrings` block plus unattended-install config — both were missing, so the site couldn't boot at all before this.
- **`Program.cs`**: reads `Mode`/`Processor` from config via two small sample-local enums (`ImageProcessingMode`, `ImageProcessorKind` — these stay in the sample, not Core, since Core must stay processor-agnostic). `InProcess` calls `AddImageProcessing(...)` bound to the config section, then `.UseSkiaSharp()` or `.UseImageFlow()` per `Processor`, and mounts `app.UseImageProcessing()` before `BootUmbracoAsync()`/`UseUmbraco()` so it intercepts `/media` ahead of Umbraco's own pipeline. `Standalone` instead wires a redirect-to-external-service middleware, ported from `imagesharp-standalone-service-plan.md` §3.2 and made processor-agnostic (reads extensions/route prefix off Core's own `ImageProcessingOptions` instead of a hardcoded set). No standalone service exists yet to redirect to (that's ticket 06 / Standalone Service) — the switch itself is fully wired and ready for when it does.
- Making the processor a **config value** (not just a code-edited DI line) was a deliberate small addition beyond the ticket's literal wording: the map's Notes require the quickstart docs to swap processors "via a config swap — not six," so the processor needs to already be config-driven by the time tickets 08/09 are written.

**Manually verified** (built + ran the sample with `dotnet run`, a real JPEG/PNG dropped straight into `wwwroot/media` since the site has no installed content yet — see gap note below; hitting the URL shape directly via `curl` exercises the exact same code path a Cropper-rendered `<img>` would, since `IImageUrlGenerator` produces that same shape):

- SkiaSharp selected: plain `/media/sample.jpg` passes through unmodified (900×600); `?width=200` resizes proportionally to 200×133; `?width=200&format=webp` converts format; `?width=300&height=300&cc=0.25,0.25,0.25,0.25` produces an exact 300×300 crop. Derivative cache populated correctly under `App_Data/image-cache`.
- Flipped `ImageProcessing:Processor` to `ImageFlow` in `appsettings.json` only (no code change) and re-ran the same checks — identical results, plus confirmed ImageFlow's `format=gif` output (SkiaSharp can't do this, per ticket 03).
- Added a `.gitignore` entry for `App_Data/` (derivative cache), matching the existing `wwwroot/media/` entry.

**Follow-up fix (same day)**: the sample was 500ing on boot (`BootFailedException`) even with the unattended-install config above in place. Root cause: the `Unattended:*` property names used were stale — copied from an older Umbraco major version. Umbraco.Cms 18.1.1's actual schema (confirmed against `appsettings-schema.Umbraco.Cms.json`) is `InstallUnattended` (bool, not `InstallUnattendedUser`), `UnattendedUserName`/`UnattendedUserEmail`/`UnattendedUserPassword` (not prefixed `InstallUnattendedUser*`), and there is no `InstallMissingDatabase` key at all in this version — `InstallUnattended: true` alone covers creating the missing SQLite file. With the corrected keys, the site now installs and boots cleanly: `/` serves Umbraco's real welcome page (200) and `/umbraco` (backoffice) responds (200), both confirmed via `curl` after a clean `dotnet run`. The image-processing middleware continues to work identically alongside a fully-booted CMS.

**Remaining known gap, out of this ticket's scope**: the site has no authored content/document types/templates yet — the manual verification above used a JPEG dropped straight into `wwwroot/media` rather than an actual Cropper-configured media picker on a real page. Full CMS click-through with real content belongs to ticket 10 (Manual Verification) once the Aspire AppHost and both quickstart docs exist.
