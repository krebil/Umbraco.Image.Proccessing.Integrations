# 02 — Package identity, lockstep versioning & licensing metadata

**What to build:** Give the four product projects (Core, SkiaSharp, ImageFlow, UmbracoExtensions) their public package identity, a single shared version, and honest per-package licensing, so `dotnet pack` on each one produces a `.nupkg` ready for nuget.org — naming, versioning, and licensing metadata only, no publish pipeline yet (that's ticket 09).

- `PackageId` = `Krebil.` + existing `AssemblyName` for all four projects. `Service`, the `Umbraco` sample, and `AppHost` are never packed.
- One `<Version>` in `Directory.Build.props`, referenced by all four packable projects (lockstep — ADR-0001).
- `PackageLicenseExpression` = MIT for Core, SkiaSharp, and UmbracoExtensions.
- ImageFlow: `PackageLicenseExpression` stays MIT for the wrapper code, but its `PackageReadmeFile` (the NuGet Gallery listing page) opens with a clear warning that the underlying `Imageflow.NET`/`Imageflow.AllPlatforms` dependency requires AGPLv3 compliance or a commercial Imazen license to execute, plus a note that third-party terms can change and readers must verify current terms themselves (ADR-0002).
- Root `LICENSE` file (MIT) added, covering the reference implementation (`Service`, `Umbraco` sample, `AppHost`) and repo tooling — distinct from and not overridden by the four packages' own licenses.
- README note clarifying that `Service`, the `Umbraco` sample, and `AppHost` are reference implementation only — not a maintained, deployable artifact (no Dockerfile/container image maintained by this project).

**Blocked by:** 01 (needs all four packable projects, including `UmbracoExtensions`, to exist).

**Status:** ready-for-agent

- [ ] `dotnet pack` succeeds for Core, SkiaSharp, ImageFlow, and UmbracoExtensions, each producing a `.nupkg` with `PackageId` `Krebil.Umbraco.Image.Processing.<Name>`
- [ ] All four packed `.nupkg`s share the same version number, sourced from a single `<Version>` in `Directory.Build.props`
- [ ] `Service`, `Umbraco` (sample), and `AppHost` are not packable (no `PackageId`/pack output, or explicitly `<IsPackable>false</IsPackable>`)
- [ ] Core, SkiaSharp, and UmbracoExtensions packages report `PackageLicenseExpression` = MIT
- [ ] ImageFlow package reports `PackageLicenseExpression` = MIT and its `PackageReadmeFile` opens with the AGPLv3/commercial-license warning and the terms-can-change note
- [ ] Root `LICENSE` file (MIT) exists at the repo root
- [ ] README documents that the reference implementation projects are not a maintained deployable and carry no Dockerfile/container image
