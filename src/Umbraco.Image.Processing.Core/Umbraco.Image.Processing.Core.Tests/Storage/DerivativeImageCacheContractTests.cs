using System.Text;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.Core.Tests.TestSupport;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Storage;

/// <summary>
/// The behavior every <see cref="IDerivativeImageCache" /> backend must satisfy: read/write/clear
/// round-trip plus TTL eviction (ADR-0007). A concrete backend's test class implements
/// <see cref="CreateCache" /> and inherits these facts unchanged — no per-backend duplication. The
/// Blob backend (ticket 05) plugs in the same way.
/// </summary>
public abstract class DerivativeImageCacheContractTests
{
    /// <summary>
    /// Creates a fresh, empty cache instance backed by <paramref name="timeProvider" /> and
    /// configured with the given <paramref name="maxAge" />.
    /// </summary>
    protected abstract IDerivativeImageCache CreateCache(TimeSpan maxAge, TimeProvider timeProvider);

    private static MemoryStream ContentStream(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task TryOpenReadAsync_MissingEntry_ReturnsNull()
    {
        IDerivativeImageCache cache = CreateCache(TimeSpan.FromDays(1), new FakeTimeProvider(DateTimeOffset.UtcNow));

        Stream? result = await cache.TryOpenReadAsync("missing-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        IDerivativeImageCache cache = CreateCache(TimeSpan.FromDays(1), new FakeTimeProvider(DateTimeOffset.UtcNow));

        await cache.WriteAsync("key", ContentStream("content"));

        await using Stream? result = await cache.TryOpenReadAsync("key");
        Assert.NotNull(result);
        using var reader = new StreamReader(result!);
        Assert.Equal("content", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ClearAsync_RemovesWrittenEntries()
    {
        IDerivativeImageCache cache = CreateCache(TimeSpan.FromDays(1), new FakeTimeProvider(DateTimeOffset.UtcNow));
        await cache.WriteAsync("key", ContentStream("content"));

        await cache.ClearAsync();

        Stream? result = await cache.TryOpenReadAsync("key");
        Assert.Null(result);
    }

    [Fact]
    public async Task TryOpenReadAsync_EntryOlderThanMaxAge_ReturnsNull()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var maxAge = TimeSpan.FromHours(1);
        IDerivativeImageCache cache = CreateCache(maxAge, timeProvider);
        await cache.WriteAsync("key", ContentStream("content"));

        timeProvider.Advance(maxAge + TimeSpan.FromSeconds(1));

        Stream? result = await cache.TryOpenReadAsync("key");
        Assert.Null(result);
    }

    [Fact]
    public async Task TryOpenReadAsync_EntryWithinMaxAge_IsStillReturned()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var maxAge = TimeSpan.FromHours(1);
        IDerivativeImageCache cache = CreateCache(maxAge, timeProvider);
        await cache.WriteAsync("key", ContentStream("content"));

        timeProvider.Advance(maxAge - TimeSpan.FromSeconds(1));

        await using Stream? result = await cache.TryOpenReadAsync("key");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task EvictExpiredAsync_PhysicallyRemovesExpiredEntries()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var maxAge = TimeSpan.FromHours(1);
        IDerivativeImageCache cache = CreateCache(maxAge, timeProvider);
        await cache.WriteAsync("key", ContentStream("content"));

        TimeSpan expiredBy = maxAge + TimeSpan.FromSeconds(1);
        timeProvider.Advance(expiredBy);
        await cache.EvictExpiredAsync();

        // Rewind past the expiry window: if EvictExpiredAsync only checked the TTL rather than
        // physically removing the entry, the clock rewind alone would make it "unexpired" again and
        // TryOpenReadAsync would return it. It must not.
        timeProvider.Advance(-expiredBy);
        Stream? result = await cache.TryOpenReadAsync("key");
        Assert.Null(result);
    }

    [Fact]
    public async Task EvictExpiredAsync_LeavesUnexpiredEntriesReadable()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var maxAge = TimeSpan.FromHours(1);
        IDerivativeImageCache cache = CreateCache(maxAge, timeProvider);
        await cache.WriteAsync("key", ContentStream("content"));

        await cache.EvictExpiredAsync();

        await using Stream? result = await cache.TryOpenReadAsync("key");
        Assert.NotNull(result);
    }
}
