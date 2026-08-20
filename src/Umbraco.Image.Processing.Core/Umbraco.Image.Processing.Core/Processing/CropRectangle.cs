namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// A pixel-space crop rectangle, already resolved from a normalized <c>cc</c> command.
/// </summary>
public readonly record struct CropRectangle(int X, int Y, int Width, int Height);
