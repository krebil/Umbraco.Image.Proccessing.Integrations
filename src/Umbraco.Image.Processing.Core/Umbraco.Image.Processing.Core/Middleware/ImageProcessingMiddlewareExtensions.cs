using Microsoft.AspNetCore.Builder;

namespace Umbraco.Image.Processing.Core.Middleware;

public static class ImageProcessingMiddlewareExtensions
{
    /// <summary>
    /// Mounts <see cref="ImageProcessingMiddleware" />. In-process, mount it into the existing Umbraco
    /// pipeline; standalone, mount it into a bare ASP.NET Core app.
    /// </summary>
    public static IApplicationBuilder UseImageProcessing(this IApplicationBuilder app) =>
        app.UseMiddleware<ImageProcessingMiddleware>();
}
