# Per-package licensing, not a repo-wide license

`Imageflow.NET`'s `InProcessAsync()` — which the ImageFlow processor calls directly — requires AGPLv3 compliance or a commercial Imazen license to actually execute a job, independent of whether `Imageflow.Server` is used. `SkiaSharp` itself is MIT, no such encumbrance. A single repo-wide license would either overstate ImageFlow's freedom or understate Core/SkiaSharp's.

Decided: each of the three published packages carries its own `PackageLicenseExpression`, as nonrestrictive as the package's own code allows:

- `Krebil.Umbraco.Image.Processing.Core` and `.SkiaSharp` — MIT, no third-party encumbrance to flag.
- `Krebil.Umbraco.Image.Processing.ImageFlow` — the wrapper code itself is MIT, but its `PackageReadmeFile` (rendered on the NuGet Gallery listing, not buried in a quickstart doc) leads with a warning that the underlying `Imageflow.NET`/`Imageflow.AllPlatforms` dependency requires AGPLv3 compliance or a commercial Imazen license to run, and explicitly notes that third-party license terms can change and the reader must verify current terms themselves rather than trust the README as current.

Rejected: excluding ImageFlow from public NuGet entirely to sidestep the legal-exposure question. That would narrow the whole point of the abstraction — pick your processor — down to a two-processor story that's already built and tested; putting the warning on the package listing itself, where a consumer can't miss it before installing, was judged sufficient.
