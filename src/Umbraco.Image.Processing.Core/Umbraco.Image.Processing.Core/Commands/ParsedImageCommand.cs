namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// The canonical command model, parsed and validated from a request's query string but not yet
/// resolved against the source image's dimensions/orientation (see
/// <c>Umbraco.Image.Processing.Core.Processing.ImageCommandResolver</c>).
/// </summary>
public sealed record ParsedImageCommand
{
    public int? Width { get; init; }

    public int? Height { get; init; }

    /// <summary>How Width/Height combine when both are set. Defaults to <see cref="ResizeMode.Crop" />, matching ImageSharp's own default.</summary>
    public ResizeMode Mode { get; init; } = ResizeMode.Crop;

    public string? Format { get; init; }

    public int? Quality { get; init; }

    public ImageColor? BackgroundColor { get; init; }

    public bool AutoOrient { get; init; } = true;

    public ImageCropCoordinates? Crop { get; init; }

    public bool HasProcessingCommands =>
        Width is not null || Height is not null || Format is not null || Quality is not null ||
        BackgroundColor is not null || Crop is not null;
}
