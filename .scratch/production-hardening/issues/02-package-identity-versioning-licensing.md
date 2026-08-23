# 02 — Package identity, lockstep versioning & licensing metadata

**What to build:** Give the four product projects (Core, SkiaSharp, ImageFlow, UmbracoExtensions) their public package identity, a single shared version, and honest per-package licensing, so `dotnet pack` on each one produces a `.nupkg` ready for nuget.org — naming, versioning, and licensing metadata only, no publish pipeline yet (that's ticket 09).

- `PackageId` = `Krebil.` + existing `AssemblyName` for all four projects. `Service`, the `Umbraco` sample, and `AppHost` are never packed.
- One `<Version>` in `Directory.Build.props`, referenced by all four packable projects (lockstep — ADR-0001).
- `PackageLicenseExpression` = MIT for Core, SkiaSharp, and UmbracoExtensions.
- ImageFlow: `PackageLicenseExpression` stays MIT for the wrapper code, but its `PackageReadmeFile` (the NuGet Gallery listing page) opens with a clear warning that the underlying `Imageflow.NET`/`Imageflow.AllPlatforms` dependency requires AGPLv3 compliance or a commercial Imazen license to execute, plus a note that third-party terms can change and readers must verify current terms themselves (ADR-0002).
- Root `LICENSE` file (MIT) added, covering the reference implementation (`Service`, `Umbraco` sample, `AppHost`) and repo tooling — distinct from and not overridden by the four packages' own licenses.
- README note clarifying that `Service`, the `Umbraco` sample, and `AppHost` are reference implementation only — not a maintained, deployable artifact (no Dockerfile/container image maintained by this project).

**Blocked by:** 01 (needs all four packable projects, including `UmbracoExtensions`, to exist).

**Status:** resolved

- [x] `dotnet pack` succeeds for Core, SkiaSharp, ImageFlow, and UmbracoExtensions, each producing a `.nupkg` with `PackageId` `Krebil.Umbraco.Image.Processing.<Name>`
- [x] All four packed `.nupkg`s share the same version number, sourced from a single `<Version>` in `Directory.Build.props`
- [x] `Service`, `Umbraco` (sample), and `AppHost` are not packable (no `PackageId`/pack output, or explicitly `<IsPackable>false</IsPackable>`)
- [x] Core, SkiaSharp, and UmbracoExtensions packages report `PackageLicenseExpression` = MIT
- [x] ImageFlow package reports `PackageLicenseExpression` = MIT and its `PackageReadmeFile` opens with the AGPLv3/commercial-license warning and the terms-can-change note
- [x] Root `LICENSE` file (MIT) exists at the repo root
- [x] README documents that the reference implementation projects are not a maintained deployable and carry no Dockerfile/container image

## Comments

- New root `Directory.Build.props` sets `<Version>1.0.0</Version>` as the single lockstep version source; auto-imported by every project in the repo (harmless for the non-packable ones).
- `PackageId`/`PackageLicenseExpression` added directly to each of the four packable `.csproj` files. `IsPackable=false` added explicitly to `Service`, `Umbraco` (sample), and `AppHost` — verified each now fails to pack with NuGet's "packaging has been disabled" warning and produces no `.nupkg`.
- ImageFlow gets its own `README.md` (packed via `PackageReadmeFile`/`PackagePath="\"`), leading with the AGPLv3/commercial-license warning about `Imageflow.NET`/`Imageflow.AllPlatforms` and a terms-can-change note, per ADR-0002.
- Root `LICENSE` (MIT, copyright Krebil — matches the `nuget.org`/GitHub author identity the `Krebil.*` package prefix and `github.com/krebil/...` remote already establish) added, with a closing note that it doesn't override the four packages' own `PackageLicenseExpression`s.
- README gained a "Quickstart" section linking `docs/quickstart-in-process.md`/`docs/quickstart-standalone.md`, and a "product vs. reference implementation" section (license pointer + no-Dockerfile note).
- **Found and fixed an unrelated pre-existing bug while verifying**: `Umbraco.Image.Processing.Core.Tests` was missing from the `.sln` entirely (predates this ticket — the `.sln` arrived already modified at the start of this session). `dotnet build`/`dotnet test` against the `.sln` were silently skipping all 61 Core unit tests. Re-added via `dotnet sln add`; confirmed all five test projects (61+10+16+19 = 106 tests) now run together via the solution. Also corrected an inaccurate claim in ticket 01's comments ("Core.Tests still has 71 passing tests post-split") that turned out to be a stale, un-rebuilt binary read via `--no-build`; the correct post-split figure is 61.
- All four `.nupkg`s verified via `unzip`ing the `.nuspec`: correct `<id>`, matching `<version>1.0.0</version>` across all four, correct `<license type="expression">MIT</license>`, and ImageFlow's `<readme>README.md</readme>` present with the file actually packed.
