using Umbraco.Image.Processing.AzureBlob.Options;
using Umbraco.Image.Processing.AzureBlob.Storage;
using Umbraco.Image.Processing.Core.Options;
using Umbraco.Image.Processing.Core.Storage;
using Umbraco.Image.Processing.Core.Tests.Storage;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Umbraco.Image.Processing.AzureBlob.Tests.Storage;

/// <summary>
/// Runs the shared <see cref="DerivativeImageCacheContractTests" /> suite from ticket 04 against the
/// Blob backend + Azurite — the same 7 facts <c>LocalDiskDerivativeImageCacheTests</c> inherits, no
/// duplicated test logic. A fresh, randomly-named container per test (xunit constructs a new test
/// class instance per test method) gives each test the isolation the contract expects, backed by one
/// shared Azurite container for the whole class (started once via <see cref="AzuriteFixture" />).
/// </summary>
public sealed class AzureBlobDerivativeImageCacheTests(AzuriteFixture fixture) : DerivativeImageCacheContractTests, IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _fixture = fixture;
    private readonly string _containerName = "derivative-cache-" + Guid.NewGuid().ToString("N");

    protected override IDerivativeImageCache CreateCache(TimeSpan maxAge, TimeProvider timeProvider) =>
        new AzureBlobDerivativeImageCache(
            MicrosoftOptions.Create(new ImageProcessingOptions { CacheControlMaxAge = maxAge }),
            MicrosoftOptions.Create(new AzureBlobCacheOptions { ConnectionString = _fixture.ConnectionString, ContainerName = _containerName }),
            timeProvider);
}
