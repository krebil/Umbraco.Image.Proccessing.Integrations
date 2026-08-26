namespace Umbraco.Image.Processing.Core.Tests.TestSupport;

/// <summary>
/// A controllable <see cref="TimeProvider" /> for TTL-eviction tests — lets a test jump an entry
/// past its max age, or rewind past an eviction pass to prove removal was physical rather than a
/// TTL-check artifact, without real delays.
/// </summary>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utcNow = start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
