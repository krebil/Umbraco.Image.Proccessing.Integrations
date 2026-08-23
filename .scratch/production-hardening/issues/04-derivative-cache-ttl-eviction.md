# 04 — Derivative cache TTL eviction (local disk) + cache-contract suite

**What to build:** The derivative cache stops growing unboundedly. Add TTL-based eviction to `IDerivativeImageCache`, driven by the existing `ImageProcessingOptions.CacheControlMaxAge` (default 365 days, currently declared but unused) — no new setting. Implement it for `LocalDiskDerivativeImageCache`: an entry older than `CacheControlMaxAge` is no longer returned by `TryOpenReadAsync` and is removed on the next eviction pass. No LRU or max-size logic — correctness never depends on eviction timing, since the existing `v` cache-buster query parameter already makes a stale entry provably unreferenced by any live URL (ADR-0007). Also stand up the shared, parameterized `IDerivativeImageCache` contract suite (read/write/clear round-trip + TTL-eviction behavior) that ticket 05's Blob backend will plug into.

**Blocked by:** None — can start immediately.

**Status:** resolved

- [x] `IDerivativeImageCache` gains an eviction member/operation
- [x] `LocalDiskDerivativeImageCache` implements eviction using `CacheControlMaxAge`
- [x] An entry older than `CacheControlMaxAge` is no longer returned by `TryOpenReadAsync`
- [x] An entry older than `CacheControlMaxAge` is physically removed after an eviction pass runs
- [x] A shared, parameterized `IDerivativeImageCache` contract test suite exists, covering read/write/clear round-trip and TTL eviction, structured so a second backend can be parameterized in without duplicating the suite
- [x] The contract suite passes against `LocalDiskDerivativeImageCache`

## Comments

- `IDerivativeImageCache` gains `Task EvictExpiredAsync(CancellationToken cancellationToken = default)`. `TryOpenReadAsync` and eviction are deliberately separate operations: the former does a lazy age check against the file it's already opening (no side effect), the latter does a full pass over the cache root deleting anything expired. This matches the ticket's two distinct checklist items and keeps the hot read path free of directory-walk cost.
- Age is tracked via each entry's filesystem `LastWriteTimeUtc` — `WriteAsync` already resets this on every write, so no new metadata/side-channel was needed.
- Added `TimeProvider` (BCL, no new package) to `LocalDiskDerivativeImageCache` — optional constructor parameter defaulting to `TimeProvider.System`, registered in `AddImageProcessing()` via `TryAddSingleton`. Keeps the class trivially testable without real delays or touching file-timestamp mtimes from test code.
- Contract suite lives at `Storage/DerivativeImageCacheContractTests.cs` in `Core.Tests`: an abstract class with one `protected abstract CreateCache(TimeSpan maxAge, TimeProvider timeProvider)` seam and 7 `[Fact]`s covering missing-key, round-trip, clear, TTL-filtered read (within/past max age), and eviction. `LocalDiskDerivativeImageCacheTests` implements the seam and inherits all 7, adding 2 local-disk-only edge cases (no-op on a missing root for both `ClearAsync` and `EvictExpiredAsync`). Ticket 05's Blob backend gets the same 7 facts for free by adding one more subclass — no suite duplication.
- The eviction-physically-removes test doesn't touch disk paths directly (would break backend-agnosticism for ticket 05's Blob subclass): it evicts past the TTL, then **rewinds** the fake clock back within the TTL window and asserts the entry still isn't readable. If eviction only did another TTL check rather than actually deleting, this would incorrectly succeed — so the rewind is what proves physical removal, not just filtering.
- Full solution build and test run after the change: 0 warnings/errors; 68/10/19/17 (Core/UmbracoExtensions/ImageFlow/SkiaSharp) all green, Core's count up from 61 (+7 new facts inherited from the contract suite, +2 local-disk-only facts, −2 tests folded into the contract suite that they duplicated).
