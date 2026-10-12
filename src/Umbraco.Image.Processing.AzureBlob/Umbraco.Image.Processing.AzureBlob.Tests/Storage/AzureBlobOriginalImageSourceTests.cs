using Azure.Storage.Blobs;
using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.AzureBlob.Storage;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.AzureBlob.Tests.Storage;

/// <summary>
/// <see cref="AzureBlobOriginalImageSource" /> reads media Umbraco's own Blob-backed media file system
/// already wrote — it never creates the container itself (that container's lifecycle belongs to
/// Umbraco). Each test therefore creates the container directly via <see cref="BlobContainerClient" />
/// to stand in for what Umbraco's media file system would have already done, then exercises the source
/// purely through its own public seam, mirroring <c>AzureBlobDerivativeImageCacheTests</c>' isolation
/// pattern (a fresh, randomly-named container per test instance, one shared Azurite emulator per class).
/// </summary>
public sealed class AzureBlobOriginalImageSourceTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _fixture = fixture;
    private readonly string _containerName = "media-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task OpenReadAsync_BlobExists_ReturnsItsSeekableContent()
    {
        var container = new BlobContainerClient(_fixture.ConnectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        byte[] expected = "not a real image, just bytes to round-trip"u8.ToArray();
        await container.GetBlobClient("1234/photo.jpg").UploadAsync(new MemoryStream(expected), overwrite: true);

        AzureBlobOriginalImageSource source = CreateSource();

        await using Stream? stream = await source.OpenReadAsync("/1234/photo.jpg");

        Assert.NotNull(stream);
        Assert.True(stream.CanSeek, "OpenReadAsync must return a seekable stream — the middleware rewinds it after header sniffing.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(expected, buffer.ToArray());

        // Prove it's genuinely rewindable after a partial read, the way the middleware uses it:
        // read a few bytes, seek back to 0, and confirm the full content is still readable.
        stream.Position = 0;
        var partial = new byte[4];
        _ = await stream.ReadAsync(partial);
        stream.Position = 0;
        using var rewound = new MemoryStream();
        await stream.CopyToAsync(rewound);
        Assert.Equal(expected, rewound.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_BlobMissing_ReturnsNull()
    {
        var container = new BlobContainerClient(_fixture.ConnectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        AzureBlobOriginalImageSource source = CreateSource();

        Stream? stream = await source.OpenReadAsync("/does-not-exist/photo.jpg");

        Assert.Null(stream);
    }

    [Fact]
    public async Task OpenReadAsync_ContainerMissing_ReturnsNull()
    {
        // No CreateIfNotExistsAsync call — proves the source doesn't create the container itself,
        // unlike AzureBlobDerivativeImageCache: its lifecycle belongs to Umbraco's media file system.
        AzureBlobOriginalImageSource source = CreateSource();

        Stream? stream = await source.OpenReadAsync("/1234/photo.jpg");

        Assert.Null(stream);
    }

    [Theory]
    [InlineData("/../secret/photo.jpg")]
    [InlineData("/x/../../secret/photo.jpg")]
    [InlineData("/./../secret/photo.jpg")]
    [InlineData("/..\\secret\\photo.jpg")]
    public async Task OpenReadAsync_DotSegments_CannotEscapeTheBlobPathPrefix(string path)
    {
        // The SDK builds the blob URI with System.Uri, which collapses "." and ".." segments: without a
        // guard, "archive/../secret/photo.jpg" is fetched as "secret/photo.jpg".
        await UploadSecretAsync();

        Assert.Null(await CreateArchiveSource().OpenReadAsync(path));
    }

    [Theory]
    [InlineData("/..%2Fsecret%2Fphoto.jpg")]
    [InlineData("/..%2fsecret%2fphoto.jpg")]
    [InlineData("/..%5Csecret%5Cphoto.jpg")]
    [InlineData("/..%252Fsecret%252Fphoto.jpg")]
    public async Task OpenReadAsync_EncodedSeparators_AreTakenLiterally_AndCannotEscapeTheBlobPathPrefix(string path)
    {
        // The matcher refuses these before any storage sees them; this pins that the SDK escapes the
        // percent sign in the blob name rather than decoding the sequence into a real separator.
        await UploadSecretAsync();

        Assert.Null(await CreateArchiveSource().OpenReadAsync(path));
    }

    private async Task UploadSecretAsync()
    {
        var container = new BlobContainerClient(_fixture.ConnectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient("secret/photo.jpg").UploadAsync(new MemoryStream("secret"u8.ToArray()), overwrite: true);
    }

    private AzureBlobOriginalImageSource CreateArchiveSource() =>
        new(MicrosoftOptions.Create(new AzureBlobOriginalImageSourceOptions
        {
            ConnectionString = _fixture.ConnectionString,
            ContainerName = _containerName,
            BlobPathPrefix = "archive",
        }));

    [Fact]
    public async Task OpenReadAsync_DefaultBlobPathPrefix_PrependsMediaToTheRequestPath()
    {
        // Confirmed against Umbraco.StorageProviders.AzureBlob's own AzureBlobFileSystem.GetBlobName:
        // with its ContainerRootPath left unset (the common case), blob names are the media file
        // system's VirtualPath (defaulted from GlobalSettings.UmbracoMediaPath, "~/media") + the
        // relative path — e.g. a request for /1234/photo.jpg is stored as the blob "media/1234/photo.jpg".
        // BlobPathPrefix defaults to "media" to match that real-world default, not the bare relative
        // path this class's other tests use (which set BlobPathPrefix to "" to isolate other behavior).
        var container = new BlobContainerClient(_fixture.ConnectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        byte[] expected = "prefixed-by-default"u8.ToArray();
        await container.GetBlobClient("media/1234/photo.jpg").UploadAsync(new MemoryStream(expected), overwrite: true);

        var source = new AzureBlobOriginalImageSource(MicrosoftOptions.Create(new AzureBlobOriginalImageSourceOptions
        {
            ConnectionString = _fixture.ConnectionString,
            ContainerName = _containerName,
            // BlobPathPrefix intentionally left at its default ("media").
        }));

        await using Stream? stream = await source.OpenReadAsync("/1234/photo.jpg");

        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(expected, buffer.ToArray());
    }

    private AzureBlobOriginalImageSource CreateSource() =>
        new(MicrosoftOptions.Create(new AzureBlobOriginalImageSourceOptions
        {
            ConnectionString = _fixture.ConnectionString,
            ContainerName = _containerName,
            // Empty prefix in these tests: they exercise read/missing/traversal semantics independent
            // of the prefix concern, which OpenReadAsync_DefaultBlobPathPrefix_... covers on its own.
            BlobPathPrefix = string.Empty,
        }));
}
