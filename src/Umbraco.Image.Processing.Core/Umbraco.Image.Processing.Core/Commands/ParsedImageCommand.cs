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

    public string? Format { get; init; }

    public int? Quality { get; init; }

    public ImageColor? BackgroundColor { get; init; }

    public bool AutoOrient { get; init; } = true;

    public ImageCropCoordinates? Crop { get; init; }

    public bool HasProcessingCommands =>
        Width is not null || Height is not null || Format is not null || Quality is not null ||
        BackgroundColor is not null || Crop is not null;
}
