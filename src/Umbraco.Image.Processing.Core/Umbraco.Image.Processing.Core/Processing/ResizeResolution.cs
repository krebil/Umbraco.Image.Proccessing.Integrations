namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// The result of <see cref="ResizeCalculator.Resolve" />: the final canvas size, plus at most one of
/// <see cref="Crop" /> (source-space, for <c>Crop</c> mode) or <see cref="Placement" />
/// (canvas-space, for <c>Pad</c>/<c>BoxPad</c>). Never both — every mode uses one, the other, or neither.
/// </summary>
public readonly record struct ResizeResolution(int Width, int Height, CropRectangle? Crop, CropRectangle? Placement);
