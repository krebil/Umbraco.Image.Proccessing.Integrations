namespace Umbraco.Image.Processing.Core.Commands;

/// <summary>
/// Matches <c>SixLabors.ImageSharp.Processing.ResizeMode</c>'s names, order, and default (<see cref="Crop" />,
/// value 0) for query-string parity: an absent <c>rmode</c> parses to the type's default value, the
/// same as ImageSharp.Web's own <c>CommandParser.ParseValue&lt;ResizeMode&gt;(null, culture)</c> falling
/// back to <c>default(ResizeMode)</c>.
/// </summary>
public enum ResizeMode
{
    /// <summary>Scales to cover the target box, then crops the overflow to the exact requested size.</summary>
    Crop,

    /// <summary>Scales to fit within the target box, padding the remainder with <c>bgcolor</c>.</summary>
    Pad,

    /// <summary>Like <see cref="Pad" />, but never upscales — a source already smaller than the target is centered unresized.</summary>
    BoxPad,

    /// <summary>Scales to fit within the target box, preserving aspect ratio. Output dimensions are the fitted size, not the requested one.</summary>
    Max,

    /// <summary>Like <see cref="Max" />, but never upscales.</summary>
    Min,

    /// <summary>Scales to the exact requested size, ignoring aspect ratio.</summary>
    Stretch,

    /// <summary>
    /// Placement is set programmatically elsewhere in ImageSharp; ImageSharp.Web's own query-string
    /// surface always resolves it to <c>(0,0,width,height)</c> — indistinguishable from
    /// <see cref="Stretch" /> when driven purely by a URL, which is this package's only entry point.
    /// </summary>
    Manual,
}
