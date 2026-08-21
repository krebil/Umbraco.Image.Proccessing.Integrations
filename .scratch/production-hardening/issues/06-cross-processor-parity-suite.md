# 06 — Cross-processor parity suite

**What to build:** Automated confidence that SkiaSharp and ImageFlow produce equivalent output for the same request, so switching processors is a config change, not a behavior change (ADR-0003). Build a shared parity test project/theory data running the full command set — resize, `cc` crop, format conversion, `bgcolor`, autoorient — against both processors via `IImageProcessor.Process()` (the same seam the existing 16 SkiaSharp / 19 ImageFlow unit tests already call). Assert equivalent output dimensions, output format, and pixel-similarity within a defined threshold — not byte-identical, since the two encoders legitimately differ. Use each processor's `SupportedFormats` (ticket 03) to determine which format cases apply to it, rather than a second hardcoded exclusion list.

**Blocked by:** 03 (needs `SupportedFormats` to know which formats to run per processor).

**Status:** ready-for-agent

- [ ] Shared parity suite exists, driven by one set of `ParsedImageCommand` theory data reused across both processors
- [ ] Covers resize, `cc` crop, format conversion, `bgcolor`, and autoorient
- [ ] For each case, asserts equivalent output dimensions and output format between SkiaSharp and ImageFlow
- [ ] For each case, asserts pixel similarity within a defined threshold (not byte-identical)
- [ ] Format cases are scoped per processor via `SupportedFormats`, not a separately maintained exclusion list
- [ ] Suite passes against the current SkiaSharp/ImageFlow implementations
