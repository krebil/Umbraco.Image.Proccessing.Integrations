# 14 — Document derivative-cache eviction scheduling

**What to build:** Ticket 13 decided *whether and how* `IDerivativeImageCache.EvictExpiredAsync` gets triggered: nothing in this repo calls it — no in-process timer, no shipped endpoint. `EvictExpiredAsync` (ticket 04/05) is already the public service surface; a consumer wires it into whatever trigger fits their own deployment. This ticket closes the *undocumented* half of that gap: right now a consumer has to independently notice `EvictExpiredAsync` exists and that nothing calls it, the same way this ticket's author did. No new code — docs only.

- Add a short "Scheduling eviction" section to both `docs/quickstart-standalone.md` and `docs/quickstart-in-process.md`:
  - `EvictExpiredAsync` exists on `IDerivativeImageCache` and removes TTL-expired entries (`CacheControlMaxAge`); nothing in this repo calls it automatically — that's a deliberate choice (ticket 13), not an oversight.
  - Wire it into a trigger that fits your own deployment: an endpoint you add and call from an external scheduler (k8s `CronJob`, Azure Function Timer, a cloud scheduler), a `BackgroundService` if you want an always-on process to own it, or any other mechanism — your choice, this repo doesn't prescribe one.
  - Local-disk (`LocalDiskDerivativeImageCache`, a directory walk) and Blob (`AzureBlobDerivativeImageCache`, a full billed container listing) may reasonably want different cadences — your call per backend, not a single fixed answer.
  - For the standalone `Service` specifically, if you're hosting it somewhere that scales to zero (Azure Container Apps, Cloud Run, etc.): whatever HTTP trigger you add for this is also what wakes the app to run the pass — that's exactly why this repo didn't bake in an always-warm in-process timer, which would have defeated scale-to-zero outright.
- Cross-reference from wherever `TryOpenReadAsync`'s existing expiry-filtering behavior is already documented (if anywhere), so a reader doesn't come away thinking eviction is fully automatic just because reads already filter expired entries.

**Blocked by:** none. Ticket 13 is the design decision this documents.

**Status:** resolved

- [x] `docs/quickstart-standalone.md` gains a "Scheduling eviction" section per the above, including the scale-to-zero note
- [x] `docs/quickstart-in-process.md` gains a "Scheduling eviction" section per the above
- [x] No new endpoint, `BackgroundService`, or other runtime code added anywhere in `src/`
- [x] Existing mentions of `EvictExpiredAsync`/cache eviction elsewhere in the docs (e.g. the Blob-container-sharing warning already in `quickstart-standalone.md`) stay consistent with the new section rather than duplicating or contradicting it

## Comments

- Docs-only change, both quickstarts. No `src/` files touched — confirmed via `git status`.
- The Blob-container-sharing warning already in `quickstart-standalone.md` (around `ClearAsync`/`EvictExpiredAsync` and container safety) is about a different concern (don't point the derivative cache at Umbraco's media container) and doesn't overlap with the new scheduling section's content, so it was left as-is rather than merged.
