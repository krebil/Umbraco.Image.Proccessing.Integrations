using Umbraco.Image.Processing.Core.Storage;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Storage;

public sealed class LocalDiskOriginalImageSourceTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "local-disk-originals-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            Directory.Delete(_parent, recursive: true);
        }
    }

    [Fact]
    public async Task FileUnderTheRoot_IsRead()
    {
        string root = await WriteAsync("media", "1234/photo.jpg");

        await using Stream? stream = await new LocalDiskOriginalImageSource(root).OpenReadAsync("/1234/photo.jpg");

        Assert.NotNull(stream);
    }

    [Fact]
    public async Task SiblingDirectorySharingTheRootsName_IsNotReachable()
    {
        // Two sources rooted side by side, e.g. ".../media" and ".../media-archive": a plain string-prefix
        // check would let the first one read the second's files.
        string root = await WriteAsync("media", "1234/photo.jpg");
        await WriteAsync("media-archive", "secret.jpg");

        Assert.Null(await new LocalDiskOriginalImageSource(root).OpenReadAsync("/../media-archive/secret.jpg"));
    }

    [Fact]
    public async Task PathEscapingTheRoot_IsNotReachable()
    {
        string root = await WriteAsync("media", "1234/photo.jpg");
        await File.WriteAllBytesAsync(Path.Combine(_parent, "outside.jpg"), [1]);

        Assert.Null(await new LocalDiskOriginalImageSource(root).OpenReadAsync("/../outside.jpg"));
    }

    [Theory]
    [InlineData("/..%2Foutside.jpg")]
    [InlineData("/..%5Coutside.jpg")]
    [InlineData("/..%252Foutside.jpg")]
    [InlineData("/1234%2F..%2F..%2Foutside.jpg")]
    public async Task EncodedSeparators_AreTakenLiterally_AndCannotEscapeTheRoot(string path)
    {
        // The matcher refuses these before any storage sees them; this pins that the storage itself
        // never decodes them either, should a host hand them over directly.
        string root = await WriteAsync("media", "1234/photo.jpg");
        await File.WriteAllBytesAsync(Path.Combine(_parent, "outside.jpg"), [1]);

        Assert.Null(await new LocalDiskOriginalImageSource(root).OpenReadAsync(path));
    }

    private async Task<string> WriteAsync(string rootName, string relativePath)
    {
        string root = Path.Combine(_parent, rootName);
        string fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, [1, 2, 3]);
        return root;
    }
}
