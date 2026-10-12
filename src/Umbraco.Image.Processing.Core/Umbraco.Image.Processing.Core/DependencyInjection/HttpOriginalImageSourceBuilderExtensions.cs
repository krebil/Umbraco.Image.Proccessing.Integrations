using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Security;
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
        // Only the unkeyed default goes; per-source (keyed) registrations stay.
        foreach (ServiceDescriptor descriptor in builder.Services.Where(d => d.ServiceType == typeof(IOriginalImageSource) && !d.IsKeyedService).ToList())
        {
            builder.Services.Remove(descriptor);
        }

        builder.Services.AddHttpClient<IOriginalImageSource, HttpOriginalImageSource>();

        return builder;
    }

    /// <summary>
    /// Fetches the originals of the source at <paramref name="routePrefix" /> from Umbraco over HTTP, as
    /// <see cref="UseHttpOriginalImageSource(IImageProcessingBuilder, Action{HttpOriginalImageSourceOptions})" />
    /// does for the default source. The path after the route prefix is what Umbraco's origin endpoint receives.
    /// </summary>
    public static IImageProcessingBuilder UseHttpOriginalImageSource(this IImageProcessingBuilder builder, string routePrefix, Action<HttpOriginalImageSourceOptions> configure)
    {
        string clientName = $"{nameof(HttpOriginalImageSource)}:{ImageSourceBuilderExtensions.KeyFor(routePrefix)}";
        builder.Services.AddHttpClient(clientName);

        return builder.AddOriginalImageSource(routePrefix, configure, (serviceProvider, options) => new HttpOriginalImageSource(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
            serviceProvider.GetRequiredService<IHmacSigner>(),
            Microsoft.Extensions.Options.Options.Create(options)));
    }
}
