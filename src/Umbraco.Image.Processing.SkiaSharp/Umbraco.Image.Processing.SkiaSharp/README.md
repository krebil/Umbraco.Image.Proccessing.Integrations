# Krebil.Umbraco.Image.Processing.SkiaSharp

An `IImageProcessor` implementation for [`Krebil.Umbraco.Image.Processing.Core`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.Core)
backed by SkiaSharp. It is a drop-in alternative to `Umbraco.Cms.Imaging.ImageSharp`, chosen with
one line of DI configuration (`UseSkiaSharp()`). This package and SkiaSharp are both MIT licensed,
so it brings no third-party license obligation.

```csharp
builder.Services.AddImageProcessing(options => { /* ImageProcessing options */ })
    .UseSkiaSharp()
    .AddUmbracoImageProcessing(); // from Krebil.Umbraco.Image.Processing.UmbracoExtensions
```

The package includes the SkiaSharp native assets for Linux, so it runs in Linux containers
without extra setup.

See the [quickstart docs](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/tree/main/docs)
in the source repository for setup, and
[`Krebil.Umbraco.Image.Processing.ImageFlow`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.ImageFlow)
for the alternative Imageflow-backed processor.
