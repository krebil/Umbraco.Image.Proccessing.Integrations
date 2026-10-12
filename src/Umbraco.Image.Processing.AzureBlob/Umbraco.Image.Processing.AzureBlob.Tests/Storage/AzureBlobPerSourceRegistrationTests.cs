using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Image.Processing.AzureBlob.DependencyInjection;
using Umbraco.Image.Processing.Core.DependencyInjection;
using Umbraco.Image.Processing.Core.Storage;
using Xunit;

namespace Umbraco.Image.Processing.AzureBlob.Tests.Storage;

public sealed class AzureBlobPerSourceRegistrationTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _fixture = fixture;

    [Fact]
    public async Task EachRoutePrefix_ReadsFromItsOwnContainerAndBlobPathPrefix()
    {
        string mediaContainer = "media-" + Guid.NewGuid().ToString("N");
        string archiveContainer = "archive-" + Guid.NewGuid().ToString("N");
        await UploadAsync(mediaContainer, "media/1234/photo.jpg", "media bytes"u8.ToArray());
        await UploadAsync(archiveContainer, "sites/abc/assets/photo.jpg", "archive bytes"u8.ToArray());

        var services = new ServiceCollection();
        services.AddImageProcessing()
            .UseAzureBlobOriginalImageSource("/media", o =>
            {
                o.ConnectionString = _fixture.ConnectionString;
                o.ContainerName = mediaContainer;
            })
            .UseAzureBlobOriginalImageSource("/-/archive/", o =>
            {
                o.ConnectionString = _fixture.ConnectionString;
                o.ContainerName = archiveContainer;
                o.BlobPathPrefix = "sites";
            });
        using ServiceProvider provider = services.BuildServiceProvider();

        IOriginalImageSource media = provider.GetRequiredKeyedService<IOriginalImageSource>("/media");
        IOriginalImageSource archive = provider.GetRequiredKeyedService<IOriginalImageSource>("/-/archive");

        Assert.Equal("media bytes"u8.ToArray(), await ReadAsync(media, "/1234/photo.jpg"));
        Assert.Equal("archive bytes"u8.ToArray(), await ReadAsync(archive, "/abc/assets/photo.jpg"));
        Assert.Null(await archive.OpenReadAsync("/1234/photo.jpg"));
        Assert.IsType<LocalDiskOriginalImageSource>(provider.GetRequiredService<IOriginalImageSource>());
    }

    private async Task UploadAsync(string containerName, string blobName, byte[] content)
    {
        var container = new BlobContainerClient(_fixture.ConnectionString, containerName);
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient(blobName).UploadAsync(new MemoryStream(content), overwrite: true);
    }

    private static async Task<byte[]> ReadAsync(IOriginalImageSource source, string path)
    {
        await using Stream? stream = await source.OpenReadAsync(path);
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
