# 13 — When does derivative cache eviction actually run?

**What to discuss:** `IDerivativeImageCache.EvictExpiredAsync` (ticket 04) physically removes expired entries, but nothing in this repo ever calls it outside tests — confirmed by grep, no `BackgroundService`/`IHostedService`/timer/cron exists anywhere in `src/`. `TryOpenReadAsync` already filters expired entries on read, so nothing stale is ever served, but without something invoking `EvictExpiredAsync`, disk/Blob usage still grows unboundedly in practice. That's short of the spec's own stated goal ("the derivative cache stops growing unboundedly," ticket 04 / spec story 16) — ticket 04 built the mechanism, not anything that triggers it.

This ticket is for deciding *whether and how* that should change before it's scoped as an implementation ticket — not a decided design yet.

**Blocked by:** none. Touches ticket 04 (owns `EvictExpiredAsync`) and ticket 05 (Blob backend, where an eviction pass costs a full container listing).

**Status:** needs-decision

## Options to weigh

1. **In-process scheduler in the reference hosts.** A `BackgroundService`/periodic timer in `Service` and the `Umbraco` sample, interval driven by config (new option, or reuse `CacheControlMaxAge` somehow). Simple, works out of the box for anyone running the reference hosts as-is. Cuts against `CONTEXT.md`'s framing of `Service`/`Umbraco`/`AppHost` as **reference implementation** — "code meant to be read and adapted... not a maintained, deployable artifact" — adding real operational behavior (a timer with failure modes, overlap handling, logging) is more than a wiring example. Worth deciding whether that framing still holds once the reference hosts do background work, or whether this is different from the "no Dockerfile" non-goal because it's in-process C#, not infra.
2. **Bring-your-own-scheduler, explicitly documented.** Leave `EvictExpiredAsync` as a public API a consumer wires to their own scheduler (k8s `CronJob`, Azure Function Timer, a hand-rolled admin trigger). No new code, but currently *undocumented* even as a responsibility — right now a consumer has to notice the gap themselves, the same way this ticket's author did. Minimum fix here is at least documenting it in the quickstarts, even if nothing else changes.
3. **Ship an admin/ops endpoint, no in-process timer.** e.g. `POST /__cache/evict` on `Service` (and equivalently for in-process mode), so a consumer's own external scheduler has something to call without reimplementing the loop themselves. Middle ground between 1 and 2 — still "bring your own scheduler" for *when*, but this repo owns the "how."
4. **Opportunistic/piggybacked eviction** — e.g. a probabilistic sweep triggered from `WriteAsync`. Cheap to reason about for local disk; likely wrong for Blob, where `EvictExpiredAsync` does a full `GetBlobsAsync` container listing — probably worth ruling out explicitly rather than silently not considering it.

## Things that should inform the decision

- Local-disk eviction (a directory walk) and Blob eviction (a full container listing, real API calls/cost) may reasonably want different default cadences — this doesn't have to be one answer for both backends.
- Whatever's decided should reconcile with the "reference implementation, not a maintained artifact" line the rest of this repo draws carefully (see `CONTEXT.md`, ADR area around ADR-0006).
- Spec story 16/17/18 (derivative cache eviction) already exists and is what ticket 04 was scoped against — check whether those stories implicitly assumed something would call this, or left it open on purpose.

## Comments

- Surfaced by the user while reviewing ticket 04/05's TTL eviction work: the mechanism is correct and tested, but literally unreachable from any running code path in this repo today.
