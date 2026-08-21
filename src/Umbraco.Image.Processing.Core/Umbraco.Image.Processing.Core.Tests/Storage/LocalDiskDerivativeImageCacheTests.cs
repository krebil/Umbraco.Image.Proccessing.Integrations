using System.Text;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Storage;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.Core.Tests.Storage;

public class LocalDiskDerivativeImageCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "image-cache-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalDiskDerivativeImageCache CreateCache() =>
        new(MicrosoftOptions.Create(new ImageProcessingOptions { DerivativeCacheRootPath = _root }));

    [Fact]
    public async Task WrittenEntryIsReadableUntilCleared()
    {
        LocalDiskDerivativeImageCache cache = CreateCache();
        await cache.WriteAsync("media/sample.jpg?width=200", new MemoryStream(Encoding.UTF8.GetBytes("content")));

        await using Stream? beforeClear = await cache.TryOpenReadAsync("media/sample.jpg?width=200");
        Assert.NotNull(beforeClear);
        beforeClear!.Dispose();

        await cache.ClearAsync();

        await using Stream? afterClear = await cache.TryOpenReadAsync("media/sample.jpg?width=200");
        Assert.Null(afterClear);
    }

    [Fact]
    public async Task ClearOnMissingRootIsANoOp()
    {
        LocalDiskDerivativeImageCache cache = CreateCache();

        await cache.ClearAsync();
    }
}
