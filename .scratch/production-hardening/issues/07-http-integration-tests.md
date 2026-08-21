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
