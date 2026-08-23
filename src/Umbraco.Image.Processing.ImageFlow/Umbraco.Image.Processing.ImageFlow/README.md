# Krebil.Umbraco.Image.Processing.ImageFlow

> ⚠️ **License notice — read before installing.** This package's own code is MIT licensed. But it
> calls `Imageflow.NET`/`Imageflow.AllPlatforms` directly to do the actual image processing, and
> *executing* an Imageflow job (`InProcessAsync()`) requires either AGPLv3 compliance in your own
> application or a commercial license from Imazen — independent of whether you use
> `Imageflow.Server`. Installing this package pulls that obligation in with it.
>
> Third-party license terms can change after this README is written. This notice is a snapshot,
> not a substitute for reading Imageflow's current license yourself before you rely on it.

An `IImageProcessor` implementation for [`Krebil.Umbraco.Image.Processing.Core`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.Core)
backed by Imageflow — a drop-in alternative to `Umbraco.Cms.Imaging.ImageSharp`, chosen with one
line of DI configuration (`UseImageFlow()`).

See the [quickstart docs](https://github.com/krebil/Umbraco.Image.Proccessing.Integrations/tree/main/docs)
in the source repository for setup, and
[`Krebil.Umbraco.Image.Processing.SkiaSharp`](https://www.nuget.org/packages/Krebil.Umbraco.Image.Processing.SkiaSharp)
for an MIT-only alternative processor with no third-party license obligation.
