# 10 — CI: tag-triggered publish workflow

**What to build:** A maintainer publishes a release by pushing a `v*` git tag against a version already committed to `Directory.Build.props` — a deliberate, auditable action, not automatic on every merge. The workflow builds, runs the full test suite (reusing ticket 09's gate), packs all four `Krebil.*` packages, and pushes to nuget.org only when everything is green.

**Blocked by:** 02 (packaging identity/licensing must be correct before anything is packed), 09 (reuses its build+test gate).

**Status:** ready-for-agent

- [ ] GitHub Actions workflow triggers on `v*` tag pushes
- [ ] Workflow builds the solution and runs the same full test suite as ticket 09's workflow
- [ ] Workflow packs all four product packages (Core, SkiaSharp, ImageFlow, UmbracoExtensions) only after tests pass
- [ ] Workflow pushes the four packages to nuget.org only on full green — any failure anywhere in build/test/pack stops the pipeline before `nuget push`
- [ ] Workflow is verified via a dry run (e.g. against a test feed or `--skip-duplicate`/local pack verification) without performing a real, irreversible publish to nuget.org
