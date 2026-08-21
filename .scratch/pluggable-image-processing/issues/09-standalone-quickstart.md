Type: task
Status: resolved
Blocked by: 06, 04

## Question

Write the standalone-deployment quickstart as its own linkable markdown file, alongside the in-process one:

- Walks through building a bare ASP.NET Core service hosting Core + a chosen processor, grounded in what the `Umbraco.Image.Processing.Service` sample project actually does.
- Documents the redirect/CDN pattern from `imagesharp-standalone-service-plan.md`, made processor-agnostic — this is the part that stays doc-only, no code, per the earlier decision that URL generation never needs to differ by processor or deployment mode.
- Covers HMAC secret sharing between the two apps, and the same "swap the processor" one-line story as the in-process doc.
- No Aspire.

## Answer

Written as [`docs/quickstart-standalone.md`](../../../docs/quickstart-standalone.md), cross-linked with the in-process quickstart.

Grounded in `Umbraco.Image.Processing.Service`'s actual `Program.cs`/`appsettings.json` (ticket 06): the whole app is `AddImageProcessing()` + `.UseSkiaSharp()`/`.UseImageFlow()` + `app.UseImageProcessing()`, no Umbraco reference. Covers the two things `ImageProcessing:Mode = Standalone` triggers on the Umbraco side — `ExternalBaseUrl`-prefixed absolute URLs for freshly generated `<img>` tags, and the redirect middleware fallback for URLs that weren't generated with the host baked in (rich text, hand-typed, direct `/media` hits) — documented as doc-only per the map's standing decision that URL generation doesn't vary by processor or mode. Same `dotnet add reference` (not `add package`) caveat as the in-process doc, HMAC secret-sharing steps (matching key on both apps, `Umbraco:CMS:Imaging:HMACSecretKey` too), the processor-swap comparison, and the ImageFlow AGPLv3/commercial-license note.

Not yet verified against a real reader walkthrough — that's ticket 10 (Manual Verification)'s job, which now has both of its quickstart dependencies (08, 09) resolved and is unblocked.
