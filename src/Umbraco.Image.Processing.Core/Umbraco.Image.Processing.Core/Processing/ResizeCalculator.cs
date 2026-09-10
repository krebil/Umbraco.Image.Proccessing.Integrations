using Umbraco.Image.Processing.Core.Commands;

namespace Umbraco.Image.Processing.Core.Processing;

/// <summary>
/// Computes the final canvas size plus an optional crop or placement rectangle for a resize mode.
/// Ported from <c>SixLabors.ImageSharp.Processing.Processors.Transforms.ResizeHelper.CalculateTargetLocationAndBounds</c>
/// (github.com/SixLabors/ImageSharp, src/ImageSharp/Processing/Processors/Transforms/Resize/ResizeHelper.cs),
/// restricted to that method's default/center-anchor branch — this package has no <c>rxy</c>/<c>ranchor</c>
/// equivalent (focal-point-aware centering is Umbraco's own <c>cc</c> command instead, applied separately
/// before this ever runs), so every mode behaves as ImageSharp's own "unspecified position" case.
/// <c>Manual</c> is routed identically to <c>Stretch</c>: ImageSharp.Web's own query-string surface always
/// resolves Manual's target rectangle to <c>(0,0,width,height)</c> — indistinguishable from Stretch when
/// driven purely by a URL, which is this package's only entry point.
/// </summary>
public static class ResizeCalculator
{
    public static ResizeResolution Resolve(int sourceWidth, int sourceHeight, int? requestedWidth, int? requestedHeight, ResizeMode mode)
    {
        if (requestedWidth is null && requestedHeight is null)
        {
            return new ResizeResolution(sourceWidth, sourceHeight, null, null);
        }

        // Populate the missing dimension so both are always concrete before the mode switch below —
        // mirrors ResizeHelper's own pre-fill step.
        int width = requestedWidth ?? Sanitize((int)MathF.Round(sourceWidth * (requestedHeight!.Value / (float)sourceHeight)));
        int height = requestedHeight ?? Sanitize((int)MathF.Round(sourceHeight * (requestedWidth!.Value / (float)sourceWidth)));

        return mode switch
        {
            ResizeMode.Crop => ResolveCrop(sourceWidth, sourceHeight, width, height),
            ResizeMode.Pad => ResolvePad(sourceWidth, sourceHeight, width, height),
            ResizeMode.BoxPad => ResolveBoxPad(sourceWidth, sourceHeight, width, height),
            ResizeMode.Max => ResolveMax(sourceWidth, sourceHeight, width, height),
            ResizeMode.Min => ResolveMin(sourceWidth, sourceHeight, width, height),
            _ => new ResizeResolution(Sanitize(width), Sanitize(height), null, null), // Stretch, Manual
        };
    }

    /// <summary>Scales to cover (width, height), then crops the source's centered overflow — the standard "object-fit: cover" rectangle, in source-pixel space.</summary>
    private static ResizeResolution ResolveCrop(int srcWidth, int srcHeight, int width, int height)
    {
        float scale = MathF.Max(width / (float)srcWidth, height / (float)srcHeight);
        int cropWidth = Math.Min(srcWidth, Sanitize((int)MathF.Round(width / scale)));
        int cropHeight = Math.Min(srcHeight, Sanitize((int)MathF.Round(height / scale)));
        int cropX = Math.Max(0, (srcWidth - cropWidth) / 2);
        int cropY = Math.Max(0, (srcHeight - cropHeight) / 2);

        return new ResizeResolution(Sanitize(width), Sanitize(height), new CropRectangle(cropX, cropY, cropWidth, cropHeight), null);
    }

    private static ResizeResolution ResolvePad(int srcWidth, int srcHeight, int width, int height)
    {
        float percentHeight = MathF.Abs(height / (float)srcHeight);
        float percentWidth = MathF.Abs(width / (float)srcWidth);

        int scaledWidth;
        int scaledHeight;
        int targetX;
        int targetY;

        if (percentHeight < percentWidth)
        {
            scaledWidth = (int)MathF.Round(srcWidth * percentHeight);
            scaledHeight = height;
            targetX = (int)MathF.Round((width - (srcWidth * percentHeight)) / 2F);
            targetY = 0;
        }
        else
        {
            scaledHeight = (int)MathF.Round(srcHeight * percentWidth);
            scaledWidth = width;
            targetY = (int)MathF.Round((height - (srcHeight * percentWidth)) / 2F);
            targetX = 0;
        }

        return new ResizeResolution(Sanitize(width), Sanitize(height), null, new CropRectangle(targetX, targetY, Sanitize(scaledWidth), Sanitize(scaledHeight)));
    }

    /// <summary>Like <see cref="ResolvePad" />, but a source already smaller than the target in both dimensions is centered unresized rather than upscaled.</summary>
    private static ResizeResolution ResolveBoxPad(int srcWidth, int srcHeight, int width, int height)
    {
        float percentHeight = MathF.Abs(height / (float)srcHeight);
        float percentWidth = MathF.Abs(width / (float)srcWidth);

        int boxPadHeight = height > 0 ? height : (int)MathF.Round(srcHeight * percentWidth);
        int boxPadWidth = width > 0 ? width : (int)MathF.Round(srcWidth * percentHeight);

        if (srcWidth < boxPadWidth && srcHeight < boxPadHeight)
        {
            int targetX = (boxPadWidth - srcWidth) / 2;
            int targetY = (boxPadHeight - srcHeight) / 2;
            return new ResizeResolution(Sanitize(boxPadWidth), Sanitize(boxPadHeight), null, new CropRectangle(targetX, targetY, srcWidth, srcHeight));
        }

        return ResolvePad(srcWidth, srcHeight, width, height);
    }

    /// <summary>Fits within (width, height) preserving aspect ratio — output dimensions are the fitted size, not necessarily the requested one.</summary>
    private static ResizeResolution ResolveMax(int srcWidth, int srcHeight, int width, int height)
    {
        float percentHeight = MathF.Abs(height / (float)srcHeight);
        float percentWidth = MathF.Abs(width / (float)srcWidth);

        float ratio = height / (float)width;
        float sourceRatio = srcHeight / (float)srcWidth;

        int targetWidth = width;
        int targetHeight = height;

        if (sourceRatio < ratio)
        {
            targetHeight = (int)MathF.Round(srcHeight * percentWidth);
        }
        else
        {
            targetWidth = (int)MathF.Round(srcWidth * percentHeight);
        }

        return new ResizeResolution(Sanitize(targetWidth), Sanitize(targetHeight), null, null);
    }

    /// <summary>Like <see cref="ResolveMax" />, but never upscales — a source already within (width, height) is returned unchanged.</summary>
    private static ResizeResolution ResolveMin(int srcWidth, int srcHeight, int width, int height)
    {
        if (width > srcWidth || height > srcHeight)
        {
            return new ResizeResolution(srcWidth, srcHeight, null, null);
        }

        int widthDiff = srcWidth - width;
        int heightDiff = srcHeight - height;
        int targetWidth = width;
        int targetHeight = height;

        if (widthDiff < heightDiff)
        {
            targetHeight = (int)MathF.Round(width * (srcHeight / (float)srcWidth));
        }
        else if (widthDiff > heightDiff)
        {
            targetWidth = (int)MathF.Round(height * (srcWidth / (float)srcHeight));
        }
        else if (height > width)
        {
            targetHeight = (int)MathF.Round(srcHeight * (width / (float)srcWidth));
        }
        else
        {
            targetWidth = (int)MathF.Round(srcWidth * (height / (float)srcHeight));
        }

        return new ResizeResolution(Sanitize(targetWidth), Sanitize(targetHeight), null, null);
    }

    private static int Sanitize(int value) => Math.Max(1, value);
}
