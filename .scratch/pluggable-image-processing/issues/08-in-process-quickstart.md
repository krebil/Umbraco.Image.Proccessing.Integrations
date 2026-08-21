Type: task
Status: resolved
Blocked by: 05, 04

## Question

Write the in-process quickstart as its own linkable markdown file (root-level or a `docs/` location good for direct blog-post/README links):

- Walks through adding Core + one processor package to an existing Umbraco site, registering it in `Program.cs`, and configuring local-disk storage — grounded in what the in-process sample project actually does, not aspirational.
- Demonstrates the "drop-in" story explicitly: show the one-line swap between `.UseSkiaSharp()`, `.UseImageFlow()`, and (for comparison) the stock `.AddUmbracoImageSharp()`.
- No Aspire — plain `dotnet add package` / `Program.cs` steps only, so a reader can apply it to their own site directly.

## Answer

Written as [`docs/quickstart-in-process.md`](../../../docs/quickstart-in-process.md).

Grounded directly in the in-process sample's `Program.cs`/`appsettings.json` (ticket 05) and Core's `ServiceCollectionExtensions`/`ImageProcessingOptions`: `AddImageProcessing()` registered after `CreateUmbracoBuilder()` (DI last-one-wins), `.UseSkiaSharp()`/`.UseImageFlow()` chained off the builder, `app.UseImageProcessing()` mounted before Umbraco's own pipeline, full options table with defaults, and the three-way `AddUmbracoImageSharp()` vs `.UseSkiaSharp()` vs `.UseImageFlow()` comparison the ticket asked for.

One deviation from the ticket's literal wording, flagged rather than silently done: the processor projects aren't published to NuGet (no `PackageId`/`IsPackable` on their `.csproj`s — confirmed by inspection), so "adding the package" is `dotnet add reference`, not `dotnet add package`. Noted explicitly in the doc rather than writing aspirational steps that wouldn't actually run; real packaging is out of scope per the map's Notes (production-hardening follow-on).

Also carried over from the sample and Core: SkiaSharp's format gap (`gif`/`bmp` unsupported; ImageFlow adds `gif`), the HMAC secret-key config note (disabled unless set, and needs the matching `Umbraco:CMS:Imaging:HMACSecretKey`), and the ImageFlow AGPLv3/commercial-license note per the map's standing instruction to flag it plainly.

Not yet verified against a real "reader" walkthrough — that's ticket 10 (Manual Verification)'s job, which explicitly re-checks both quickstarts against what's checked in.
