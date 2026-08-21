# 08 — Fix build warnings and apply IDE code-style suggestions

**What to build:** A whole-codebase mechanical cleanup pass, run once the structural and feature work (splits, new processors' capability query, both cache backends, both new test suites) has landed, so it sweeps the finished state instead of being redone after each subsequent ticket. Eliminate all compiler warnings across every project in the solution, and apply current-generation IDE code-style suggestions consistently (e.g. primary constructors where applicable, collection expressions, other analyzer-suggested modernizations) — mechanical, behavior-preserving changes only, no functional edits. This is a **wide refactor** in the sense that its blast radius spans the whole codebase, but it's low-risk (style/warnings only) so it lands as a single ticket rather than expand-contract batches.

**Blocked by:** 01, 03, 04, 05, 06, 07 (sweeps the code those tickets add/move, so it runs once, last, before the codebase's warning/style baseline is locked in by CI).

**Status:** ready-for-agent

- [ ] Solution builds with zero compiler warnings across all projects (Core, UmbracoExtensions, SkiaSharp, ImageFlow, Service, Umbraco sample, AppHost, and all test projects)
- [ ] Current IDE/analyzer style suggestions are applied consistently (primary constructors where applicable, and other flagged modernizations)
- [ ] No behavior change — full test suite (existing + parity + cache-contract + integration) still passes after the sweep
- [ ] `Directory.Build.props` (or equivalent) is updated so warnings are treated as errors going forward, preventing regression
