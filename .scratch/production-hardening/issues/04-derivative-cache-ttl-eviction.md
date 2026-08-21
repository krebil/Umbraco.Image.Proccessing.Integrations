# 04 — Derivative cache TTL eviction (local disk) + cache-contract suite

**What to build:** The derivative cache stops growing unboundedly. Add TTL-based eviction to `IDerivativeImageCache`, driven by the existing `ImageProcessingOptions.CacheControlMaxAge` (default 365 days, currently declared but unused) — no new setting. Implement it for `LocalDiskDerivativeImageCache`: an entry older than `CacheControlMaxAge` is no longer returned by `TryOpenReadAsync` and is removed on the next eviction pass. No LRU or max-size logic — correctness never depends on eviction timing, since the existing `v` cache-buster query parameter already makes a stale entry provably unreferenced by any live URL (ADR-0007). Also stand up the shared, parameterized `IDerivativeImageCache` contract suite (read/write/clear round-trip + TTL-eviction behavior) that ticket 05's Blob backend will plug into.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] `IDerivativeImageCache` gains an eviction member/operation
- [ ] `LocalDiskDerivativeImageCache` implements eviction using `CacheControlMaxAge`
- [ ] An entry older than `CacheControlMaxAge` is no longer returned by `TryOpenReadAsync`
- [ ] An entry older than `CacheControlMaxAge` is physically removed after an eviction pass runs
- [ ] A shared, parameterized `IDerivativeImageCache` contract test suite exists, covering read/write/clear round-trip and TTL eviction, structured so a second backend can be parameterized in without duplicating the suite
- [ ] The contract suite passes against `LocalDiskDerivativeImageCache`
