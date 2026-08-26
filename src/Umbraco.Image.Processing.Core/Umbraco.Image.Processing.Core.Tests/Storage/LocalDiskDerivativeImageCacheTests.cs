using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.Core.Tests.TestSupport;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.Core.Tests.Storage;

public class LocalDiskDerivativeImageCacheTests : DerivativeImageCacheContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "image-cache-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected override IDerivativeImageCache CreateCache(TimeSpan maxAge, TimeProvider timeProvider) =>
        new LocalDiskDerivativeImageCache(
            MicrosoftOptions.Create(new ImageProcessingOptions { DerivativeCacheRootPath = _root, CacheControlMaxAge = maxAge }),
            timeProvider);

    [Fact]
    public async Task ClearOnMissingRootIsANoOp()
    {
        IDerivativeImageCache cache = CreateCache(TimeSpan.FromDays(1), new FakeTimeProvider(DateTimeOffset.UtcNow));

        await cache.ClearAsync();
    }

    [Fact]
    public async Task EvictExpiredOnMissingRootIsANoOp()
    {
        IDerivativeImageCache cache = CreateCache(TimeSpan.FromDays(1), new FakeTimeProvider(DateTimeOffset.UtcNow));

        await cache.EvictExpiredAsync();
    }
}
