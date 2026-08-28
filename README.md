# Umbraco.Image.Proccessing.Integrations

A pluggable image-processing abstraction for Umbraco.

## Quickstart

- [In-process quickstart](docs/quickstart-in-process.md): run image processing inside your
  Umbraco app.
- [Standalone quickstart](docs/quickstart-standalone.md): run image processing as a separate
  deployment.

## The product vs. the reference implementation

The published packages (`Krebil.Umbraco.Image.Processing.Core`, `.SkiaSharp`, `.ImageFlow`, and
`.UmbracoExtensions`) are what's versioned, tested, and supported. See
[`LICENSE`](LICENSE) for the repo's own tooling and reference-implementation license, and each
package's own `PackageLicenseExpression`/listing on nuget.org for its terms.

`Umbraco.Image.Processing.Service`, the `Umbraco` sample site, and
`Umbraco.Image.Processing.AppHost` are **reference implementation**: code meant to be read and
adapted to your own deployment. No Dockerfile or container image is maintained for them.

