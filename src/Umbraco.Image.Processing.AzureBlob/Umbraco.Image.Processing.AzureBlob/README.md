# Krebil.Umbraco.Image.Processing.AzureBlob

Azure Blob Storage support for [`Krebil.Umbraco.Image.Processing.Core`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.Core):

- `UseAzureBlobOriginalImageSource(…)` reads original images from a Blob container, for example
  the container that Umbraco's media file system writes to. There is no shared disk between
  Umbraco and the image service.
- `UseAzureBlobOriginalImageSource(prefix, …)` does the same for one image source, so several
  sources can read from different containers or blob path prefixes.
- `UseAzureBlobDerivativeCache(…)` stores processed images in a Blob container instead of on
  local disk.

```csharp
builder.Services.AddImageProcessing(options => { /* ImageProcessing options */ })
    .UseSkiaSharp()
    .UseAzureBlobOriginalImageSource("/media", options => { /* container, blob path prefix */ })
    .UseAzureBlobDerivativeCache(options => { /* cache container */ });
```

See the [standalone quickstart](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/blob/main/docs/quickstart-standalone.md)
in the source repository for configuration, including several sources served from different
containers.
