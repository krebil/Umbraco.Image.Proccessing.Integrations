using Microsoft.Extensions.Options;
using Umbraco.Image.Processing.Core.Routing;

namespace Umbraco.Image.Processing.Core.Options;

/// <summary>
/// Fails the host at start-up, naming the source, when <see cref="ImageProcessingOptions.Sources" /> could
/// not be matched against (see <see cref="ImageSourceMatcher.Validate" />), rather than when the middleware
/// is first built.
/// </summary>
internal sealed class ImageProcessingOptionsValidator : IValidateOptions<ImageProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, ImageProcessingOptions options)
    {
        IReadOnlyList<string> problems = ImageSourceMatcher.Validate(options);
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
