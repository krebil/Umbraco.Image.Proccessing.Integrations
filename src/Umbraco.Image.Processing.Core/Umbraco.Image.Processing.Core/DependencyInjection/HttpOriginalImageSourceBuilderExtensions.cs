using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Storage;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

public static class HttpOriginalImageSourceBuilderExtensions
{
    /// <summary>
    /// Swaps the original-image source from Core's default (local disk) to
    /// <see cref="HttpOriginalImageSource" />, for deployments where Umbraco and the standalone Service
    /// are separate processes with no shared disk/volume and Umbraco's media isn't Blob-backed either —
    /// the Service fetches originals from Umbraco itself over HTTP instead. See
    /// <see cref="HttpOriginalImageSource" />'s own remarks for how this avoids looping back through
    /// Umbraco's Standalone-mode redirect middleware.
    /// </summary>
    public static IImageProcessingBuilder UseHttpOriginalImageSource(this IImageProcessingBuilder builder, Action<HttpOriginalImageSourceOptions> configure)
    {
        builder.Services.AddOptions<HttpOriginalImageSourceOptions>().Configure(configure);

        // AddImageProcessing() already registers LocalDiskOriginalImageSource via TryAddSingleton.
        // AddHttpClient<TClient, TImplementation> below performs its own Add (not TryAdd) for
        // IOriginalImageSource, so — unlike the single Services.Replace(...) call tickets 05/11 use —
        // the old registration is removed explicitly first: RemoveAll-then-Add is exactly what Replace
        // does internally, spelled out here because AddHttpClient doesn't hand back a plain
        // ServiceDescriptor to pass to Replace directly.
        builder.Services.RemoveAll<IOriginalImageSource>();
        builder.Services.AddHttpClient<IOriginalImageSource, HttpOriginalImageSource>();

        return builder;
    }
}
