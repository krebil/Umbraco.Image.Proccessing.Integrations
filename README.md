# Umbraco.Image.Proccessing.Integrations

A pluggable image-processing abstraction for Umbraco.

## Quickstart

- [In-process quickstart](docs/quickstart-in-process.md): run image processing inside your
  Umbraco app.
- [Standalone quickstart](docs/quickstart-standalone.md): run image processing as a separate
  deployment.

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

