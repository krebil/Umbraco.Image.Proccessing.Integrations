# 03 — IImageProcessor.SupportedFormats capability query

**What to build:** A caller of `IImageProcessor.Process()` can ask a processor which output formats it actually supports, instead of discovering a gap only via a `NotSupportedException` at request time. Add a capability member (e.g. `SupportedFormats`) to `IImageProcessor`, implemented by both processors against the encoder gaps already known from building them: SkiaSharp excludes `gif`/`bmp`; ImageFlow excludes `bmp` (but does support `gif`). This is a breaking interface addition (ADR-0004), so it lands now, before packaging locks the public API in.

**Blocked by:** None — can start immediately.

**Status:** resolved

- [x] `IImageProcessor` exposes a member describing the set of output formats the processor supports
- [x] SkiaSharp's implementation reports exactly `jpg`/`jpeg`/`png`/`webp` (no `gif`, no `bmp`)
- [x] ImageFlow's implementation reports its supported set including `gif`, excluding `bmp`
- [x] Tests assert, per processor, that every format in the declared set actually succeeds and every format outside it actually throws `NotSupportedException` — no drift between the declared set and real behavior
- [x] Existing SkiaSharp/ImageFlow unit test suites still pass

**Resolution:** `IImageProcessor.SupportedOutputFormats` (Core), and both processors' `EncodableFormats` sets and format-succeeds tests, already existed from the original build — landed before this ticket was written. The one gap: SkiaSharp's unsupported-format test only covered `gif`, leaving `bmp` untested against real behavior. Turned `ProcessAsync_UnsupportedFormat_Throws` into a `[Theory]` covering both `gif` and `bmp`. SkiaSharp suite: 17/17 passing (was 16). ImageFlow suite: 19/19 passing, unchanged.
