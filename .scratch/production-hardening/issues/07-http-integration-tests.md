# 07 — HTTP-level integration tests (Service + in-process sample)

**What to build:** Automated coverage that catches middleware wiring/registration regressions (mounted in the wrong order, DI misregistration) that unit tests at the processor/cache seam can't see. Add `WebApplicationFactory`-based integration tests for both hosts — the standalone `Umbraco.Image.Processing.Service` and the in-process `Umbraco` sample's middleware pipeline — covering pass-through, resize, format conversion, `cc` crop, and HMAC-signed request accept / tampered-or-unsigned reject, all at the status-code/header/content-type level. Pixel correctness is not re-asserted here — that's the parity suite's job (ticket 06) at the lower seam.

**Blocked by:** 01 (needs the post-split DI/middleware wiring — Core + UmbracoExtensions — to be final before testing against it).

**Status:** ready-for-agent

- [ ] Integration test project(s) use `WebApplicationFactory` against both `Umbraco.Image.Processing.Service` and the in-process `Umbraco` sample
- [ ] Pass-through request returns the original image unmodified
- [ ] Resize request returns correct status/content-type with the expected dimensions reflected in a successful response
- [ ] Format-conversion request returns the correct `Content-Type`
- [ ] `cc` crop request succeeds at the HTTP level
- [ ] A correctly HMAC-signed request is accepted
- [ ] A tampered or unsigned request is rejected with the correct status code
- [ ] Tests run against both hosts without duplicating the same assertions inline per host (shared test helpers where the two pipelines overlap)

## Comments

- **Head start, not a claim**: in response to a direct user request (not this ticket being picked up), `src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service.Tests` now exists with the `WebApplicationFactory<Program>` scaffolding (`Program.cs` gained the `public partial class Program;` marker top-level statements need for this) and one real test: `MediaResolutionTests.ImageSavedTheWayUmbracoSavesIt_IsResolvableThroughTheStandaloneService` — writes a file at the exact relative path Umbraco's own `UniqueMediaPathScheme` (confirmed as Umbraco.Cms 18's real default `IMediaPathScheme`) would compute, builds the request URL via the real `ImageProcessingUrlGenerator`, and asserts a resize request against the standalone Service resolves and processes it correctly — plus a negative control (`ImageAtWrongRelativePath_IsNotResolvable`, asserts 404) proving the positive case is actually contingent on path agreement. This covers this ticket's "resize" and "HMAC accept" bullets for the Service host only.
- **Not covered yet**: pass-through, format conversion, `cc` crop, tampered-or-unsigned reject, the in-process `Umbraco` sample host, and shared test helpers across both hosts. Whoever picks this ticket up should extend `Service.Tests` rather than start a parallel project, and still needs an equivalent `WebApplicationFactory` project for the `Umbraco` sample.
