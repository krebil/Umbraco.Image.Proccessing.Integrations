# Krebil.Umbraco.Image.Processing.UmbracoExtensions

Connects [`Krebil.Umbraco.Image.Processing.Core`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.Core)
to Umbraco. `AddUmbracoImageProcessing()` replaces Umbraco's own `IImageUrlGenerator` and
`IImageDimensionExtractor`, so crop URLs, `GetCropUrl()` and media dimensions go through this
pipeline instead of `Umbraco.Cms.Imaging.ImageSharp`. Generated URLs are HMAC-signed when signing
is enabled, and are made absolute (`ExternalBaseUrl`) only for paths that a configured image source
serves.

```csharp
builder.Services.AddImageProcessing(options => { /* ImageProcessing options */ })
    .UseSkiaSharp() // or .UseImageFlow()
    .AddUmbracoImageProcessing();
```

`AddUmbracoImageProcessing()` uses `Replace`, not `TryAdd`, so it wins regardless of where it runs
relative to Umbraco's own registration.

See the [quickstart docs](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/tree/main/docs)
in the source repository for in-process and standalone setup.
