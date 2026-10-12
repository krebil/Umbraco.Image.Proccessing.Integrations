# Umbraco Image Processing Integrations

A pluggable image-processing abstraction for Umbraco: a shared Core defines the processor seam, command parsing, and HMAC signing; SkiaSharp and ImageFlow implement it as drop-in replacements for `Umbraco.Cms.Imaging.ImageSharp`.

## Language

**The product**:
The four published NuGet packages — `Krebil.Umbraco.Image.Processing.Core`, `.SkiaSharp`, `.ImageFlow`, `.UmbracoExtensions`. These are what's versioned, tested to the parity bar, and supported.
_Avoid_: "the library", "the packages" (ambiguous about which projects count)

**UmbracoExtensions**:
The product package implementing Umbraco's own `IImageUrlGenerator`/`IImageDimensionExtractor` interfaces. The only product package with an `Umbraco.Cms.Core` dependency — Core, SkiaSharp, and ImageFlow are Umbraco-agnostic. Required for in-process mode; irrelevant to standalone. See ADR-0006.

**Reference implementation**:
`Umbraco.Image.Processing.Service` (standalone host), the `Umbraco` sample site, and `Umbraco.Image.Processing.AppHost`. Code meant to be read and adapted by consumers to their own deployment, not a maintained, deployable artifact of this project. No Dockerfile or container image is maintained for these — consumers self-host by adapting the code.
_Avoid_: "the app", "the service" used interchangeably with the product — always distinguish which side of the line something is on.

**Processor**:
An `IImageProcessor` implementation (SkiaSharp, ImageFlow) that decodes, transforms, and encodes an image. Owns only the decode/transform/encode step; Core owns command parsing, crop-rectangle/orientation math, and signing.
_Avoid_: "engine", "backend" (backend collides with storage backend)

**Image source**:
One route the middleware serves images under, listed in `ImageProcessing:Sources` (`ImageSourceOptions`): a route prefix (whole segments, case-insensitive, the longest match wins), an optional path pattern the path after the prefix must match in full, and its own original-image storage, registered under the route prefix. `ImageSourceMatcher` is the one rule that picks the source for a request path, shared by the middleware, the URL generator, and the sample's redirect and origin endpoint; a path whose prefix matched but which cannot be checked is rejected, never passed on. With no sources configured there is one, `/media`, on the default storage.
_Avoid_: "source" on its own when the original image is meant; "media source" (Umbraco's media is one image source among several); "storage mode" outside the reference service's `Storage:Mode` setting.

**Original image**:
The unprocessed file an image source reads and the processor decodes, as the owning app stored it. Read through the source's original-image storage, an `IOriginalImageSource`: local disk, Azure Blob, or an HTTP proxy to Umbraco's origin endpoint.
_Avoid_: "source image" (collides with image source), "originals" on its own (say "original images"), "the original" without saying of what.

**Cache buster**:
The `v` query parameter `ImageProcessingUrlGenerator` writes from `options.CacheBusterValue` into every generated URL, changing whenever the original image does. Makes the derivative cache key self-invalidating — a stale `v` means the cached entry is unreferenced by any live URL, not stale-but-still-served. See ADR-0007.

**Lockstep versioning**:
All three product packages always ship the same version number on every release, even if only one changed. See ADR-0001.
