# Krebil.Umbraco.Image.Processing.Core

The processor-agnostic core of a pluggable image-processing pipeline for Umbraco: the request
middleware, command parsing, HMAC signing, derivative caching, and the original-image sources it
reads from (local disk, or HTTP from Umbraco). It does no image processing itself. Add a
processor package to go with it:

- [`Krebil.Umbraco.Image.Processing.SkiaSharp`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.SkiaSharp), MIT only.
- [`Krebil.Umbraco.Image.Processing.ImageFlow`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.ImageFlow). Read its license notice before you install it.

```csharp
builder.Services.AddImageProcessing(options => { /* ImageProcessing options */ })
    .UseSkiaSharp();

app.UseImageProcessing();
```

One instance can serve several image sources, configured under `ImageProcessing:Sources`. Each
source has its own route prefix, an optional path pattern, and its own original-image storage
(`UseLocalDiskOriginalImageSource(prefix, …)`, `UseHttpOriginalImageSource(prefix, …)`, or
`AddOriginalImageSource(prefix, factory)`).

For Umbraco integration (URL generation and dimension extraction), add
[`Krebil.Umbraco.Image.Processing.UmbracoExtensions`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.UmbracoExtensions).
For Azure Blob originals or derivative caching, add
[`Krebil.Umbraco.Image.Processing.AzureBlob`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.AzureBlob).

See the [quickstart docs](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/tree/main/docs)
for in-process and standalone setup, and the
[repository README](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations#readme) for
upgrading from 2.x.
