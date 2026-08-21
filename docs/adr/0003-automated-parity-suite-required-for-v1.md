# Automated parity suite is required for v1, not a fast-follow

The POC's map explicitly scoped out an automated parity/output test suite — manual click-through was the bar, and unit tests (64 Core / 16 SkiaSharp / 19 ImageFlow) only cover each processor in isolation. Nothing currently proves SkiaSharp and ImageFlow produce equivalent output for the same command, and nothing exercises the Service's or in-process sample's middleware pipeline beyond manual `curl` checks.

Decided: v1's test bar adds (1) a shared parity suite that runs the full command set (resize/crop/format/bgcolor/autoorient) against both processors and asserts equivalent output — dimensions, format, pixel-similarity threshold, not byte-identical, since encoders legitimately differ — and (2) integration tests for the Service's and in-process middleware pipelines. This ships as part of v1, not after.

Considered shipping v1 with only the existing unit tests and adding parity coverage as a fast-follow. Rejected: the actual risk in a multi-implementation abstraction isn't untested lines, it's processors silently drifting apart in behavior (e.g. a SkiaSharp update changing default JPEG quality) — a numeric coverage gate wouldn't catch that either, only an explicit parity suite does, and it's exactly the guarantee a "drop-in replacement, pick your processor" package needs to make credibly before publishing.
