using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace ImageProcessingDemo;

/// <summary>
/// One endpoint that saves a posted file as real Umbraco media, exclusively for the production-hardening
/// ticket 11 end-to-end test project (<c>Umbraco.Image.Processing.E2E.Tests</c>). It exists so that test
/// project — which drives this app as a genuinely separate process via Aspire, not in-process — can
/// exercise Umbraco's real media-saving code path (<see cref="IMediaService" />, the real configured
/// <c>IMediaPathScheme</c>, the real underlying media file system — local disk or Blob) over HTTP,
/// instead of hand-writing a file at a guessed path. Driving the full backoffice Management API's OAuth
/// login flow purely to reach the same <c>ContentExtensions.SetValue(...)</c> call this endpoint makes
/// directly would add a large amount of test-harness complexity for no additional coverage of the thing
/// actually under test (media resolution, not backoffice auth) — this is the same real save path either
/// route would exercise.
/// </summary>
internal static class E2ETestSupportEndpoints
{
    public static void MapE2ETestSupportEndpoints(this WebApplication app)
    {
        app.MapPost("/e2e-test-support/media", async (
            HttpRequest request,
            IMediaService mediaService,
            MediaFileManager mediaFileManager,
            MediaUrlGeneratorCollection mediaUrlGenerators,
            IShortStringHelper shortStringHelper,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider) =>
        {
            string filename = request.Query["filename"].FirstOrDefault() ?? "e2e-test-image.jpg";

            IMedia media = mediaService.CreateMediaWithIdentity(filename, Constants.System.Root, Constants.Conventions.MediaTypes.Image);

            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);
            buffer.Position = 0;

            media.SetValue(mediaFileManager, mediaUrlGenerators, shortStringHelper, contentTypeBaseServiceProvider, Constants.Conventions.Media.File, filename, buffer);
            mediaService.Save(media);

            // The same lookup ContentExtensions.SetUploadFile itself uses internally (to find any
            // previous file when overwriting) — here used the same way any consumer would, to recover
            // the URL-shaped path (e.g. "/media/1234/photo.jpg") the save just produced.
            return media.TryGetMediaPath(Constants.Conventions.Media.File, mediaUrlGenerators, out string? relativeUrl) && relativeUrl is not null
                ? Results.Text(relativeUrl, "text/plain")
                : Results.Problem("Media was saved but no file URL was recorded on it.", statusCode: StatusCodes.Status500InternalServerError);
        });
    }
}
