using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Image.Processing.Core.DependencyInjection;

/// <summary>
/// Returned by <c>AddImageProcessing()</c>. Processor packages extend this with their own
/// <c>UseSkiaSharp()</c>/<c>UseImageFlow()</c> methods that register an <c>IImageProcessor</c> — the
/// one-line swap the whole abstraction exists to enable.
/// </summary>
public interface IImageProcessingBuilder
{
    IServiceCollection Services { get; }
}
