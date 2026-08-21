# 03 — IImageProcessor.SupportedFormats capability query

**What to build:** A caller of `IImageProcessor.Process()` can ask a processor which output formats it actually supports, instead of discovering a gap only via a `NotSupportedException` at request time. Add a capability member (e.g. `SupportedFormats`) to `IImageProcessor`, implemented by both processors against the encoder gaps already known from building them: SkiaSharp excludes `gif`/`bmp`; ImageFlow excludes `bmp` (but does support `gif`). This is a breaking interface addition (ADR-0004), so it lands now, before packaging locks the public API in.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] `IImageProcessor` exposes a member describing the set of output formats the processor supports
- [ ] SkiaSharp's implementation reports exactly `jpg`/`jpeg`/`png`/`webp` (no `gif`, no `bmp`)
- [ ] ImageFlow's implementation reports its supported set including `gif`, excluding `bmp`
- [ ] Tests assert, per processor, that every format in the declared set actually succeeds and every format outside it actually throws `NotSupportedException` — no drift between the declared set and real behavior
- [ ] Existing SkiaSharp/ImageFlow unit test suites still pass
