using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Umbraco.Image.Processing.AzureBlob.Tests.Storage;

/// <summary>
/// Starts a single Azurite resource for the whole test class via the same Aspire API the real
/// <c>Umbraco.Image.Processing.AppHost</c> uses (<c>AddAzureStorage().RunAsEmulator()</c>) — one
/// emulator-orchestration mechanism for both local dev and automated tests, rather than a second,
/// separate one just for tests. Still backed by Docker underneath (Azurite only ships as a
/// container), but driven through Aspire's app model instead of a bespoke container-library
/// dependency. xunit's <see cref="IClassFixture{TFixture}" /> natively supports
/// <see cref="IAsyncLifetime" /> fixtures, so this starts once per test class, not once per test.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Umbraco_Image_Processing_AzureBlob_TestHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        ConnectionString = await _app.GetConnectionStringAsync("blobs")
            ?? throw new InvalidOperationException("Azurite emulator did not publish a 'blobs' connection string.");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
