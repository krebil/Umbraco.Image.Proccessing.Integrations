# Umbraco.Image.Proccessing.Integrations

A pluggable image-processing abstraction for Umbraco.

## Quickstart

- [In-process quickstart](docs/quickstart-in-process.md): run image processing inside your
  Umbraco app.
- [Standalone quickstart](docs/quickstart-standalone.md): run image processing as a separate
  deployment.

## Sources

One instance can serve images from several places. `ImageProcessing:Sources` lists them: each
source has a `RoutePrefix` (longest match wins), an optional `PathPattern` that gates the path after
the prefix, and its own original-image storage (local disk, Azure Blob, or HTTP proxy to Umbraco).
With no sources configured, `/media` is served from local disk. See
[Several sources](docs/quickstart-standalone.md#several-sources-serving-a-second-container).

### Upgrading from 2.x

3.0 replaces the single `ImageProcessing:RoutePrefix` and the service's `ImageProcessing:Storage`
and `ImageProcessing:Proxy` settings with `ImageProcessing:Sources`:

| 2.x | 3.0 |
|---|---|
| `RoutePrefix` | `Sources:N:RoutePrefix` |
| `Storage:Mode` | `Sources:N:Storage:Mode` |
| `Storage:AzureBlob:*` | `Sources:N:Storage:AzureBlob:*` |
| `Proxy:UmbracoBaseUrl` | `Sources:N:Storage:HttpProxy:UmbracoBaseUrl` |
| `OriginalsRootPath` (service) | `Sources:N:Storage:LocalDisk:RootPath` |
| `/__image-origin/{path}` (the origin endpoint's request shape in HTTP-proxy mode) | `/__image-origin{routePrefix}/{path}`: the endpoint receives the source's full path and resolves that source, see the [standalone quickstart](docs/quickstart-standalone.md#http-proxy-mode-fetching-originals-from-umbraco-over-http) |

Upgrade both sides together: a 3.0 service sends the new origin request
shape, which a 2.x-style endpoint resolves against the wrong path.

In code, `UseAzureBlobOriginalImageSource` and `UseHttpOriginalImageSource` gain overloads that
take the route prefix they serve (the overloads without one still replace the default source), and
`UseLocalDiskOriginalImageSource(routePrefix, …)` and `AddOriginalImageSource(routePrefix, …)` are
new; the latter also comes as `AddOriginalImageSource<TOptions>(routePrefix, configure, factory)`,
the shape the three `Use…` overloads are built on. `LocalDiskOriginalImageSource` now takes its
root path directly. `ImageProcessingUrlGenerator` only makes URLs absolute (`ExternalBaseUrl`) for
paths a configured source serves. Derivative cache keys now include the route prefix, so existing
cached derivatives are regenerated once. URLs and HMAC signatures are unchanged.

## Supported processing commands

The command surface matches ImageSharp.Web's `width`, `height`, `rmode`, `format`,
`quality`, `bgcolor`, and `autoorient`, plus Umbraco's own `cc` crop/focal-point
command.

Not yet supported, tracked as GitHub issues:
[`ranchor`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/3) (resize anchor),
[`rxy`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/4) (resize focal point),
[`rcolor`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/5) (resize pad color),
[`rsampler`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/6) (resize resampler),
[`orient`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/7) (resize orientation flag), and
[`compand`](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/issues/8) (linear-light companding).
See [ImageSharp.Web's processing commands docs](https://docs.sixlabors.com/articles/imagesharp.web/processingcommands.html)
for what each one does.

## The product vs. the reference implementation

The published packages (`Krebil.Umbraco.Image.Processing.Core`, `.SkiaSharp`, `.ImageFlow`, and
`.UmbracoExtensions`) are what's versioned, tested, and supported. See
[`LICENSE`](LICENSE) for the repo's own tooling and reference-implementation license, and each
package's own `PackageLicenseExpression`/listing on nuget.org for its terms.

`Umbraco.Image.Processing.Service`, the `Umbraco` sample site, and
`Umbraco.Image.Processing.AppHost` are **reference implementation**: code meant to be read and
adapted to your own deployment. No Dockerfile or container image is maintained for them.

