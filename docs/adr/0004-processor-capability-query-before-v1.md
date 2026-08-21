# IImageProcessor gains a format-capability query before v1

SkiaSharp's encoder doesn't support `gif`/`bmp`; ImageFlow doesn't support `bmp` but does support `gif`. Both gaps currently surface only as a `NotSupportedException` at request time. `IImageProcessor` is about to become public API across three lockstep-versioned packages, and adding a capability member later would be a breaking change forcing a major-version bump on all three, not just the one that needed it.

Decided: add a capability member to `IImageProcessor` (e.g. `SupportedFormats`) before v1 ships, so callers and the parity suite (ADR-0003) can check support up front instead of discovering it via exception.

Considered leaving it as documented runtime-exception behavior and adding a query later if real usage demanded it. Rejected: the format gaps are already known facts from building both processors, not a speculative future need — the interface-shape cost is paid once now versus a coordinated three-package major bump later.
