using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Umbraco.Tests;

/// <summary>
/// Boots the real in-process <c>Umbraco</c> sample (production-hardening ticket 07) exactly once for
/// the whole test class via <see cref="IClassFixture{TFixture}" /> — a full <c>BootUmbracoAsync</c>
/// (unattended SQLite install + migrations) is too slow to pay per test method, and nothing about these
/// pipeline tests needs a fresh Umbraco instance per request; they only need a fresh source image per
/// request, written directly under <see cref="MediaRoot" />.
/// </summary>
public sealed class UmbracoWebAppFixture : IAsyncLifetime
{
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "umbraco-pipeline-tests-media-" + Guid.NewGuid().ToString("N"));

    public string CacheRoot { get; } = Path.Combine(Path.GetTempPath(), "umbraco-pipeline-tests-cache-" + Guid.NewGuid().ToString("N"));

    public byte[] HmacSecretKey { get; } = RandomNumberGenerator.GetBytes(32);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "umbraco-pipeline-tests-db-" + Guid.NewGuid().ToString("N"), "Umbraco.sqlite.db");

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(MediaRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        string dbConnectionString = $"Data Source={_dbPath};Cache=Shared;Foreign Keys=True;Pooling=True";

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageProcessing:Mode"] = "InProcess",
                ["ImageProcessing:OriginalsRootPath"] = MediaRoot,
                ["ImageProcessing:DerivativeCacheRootPath"] = CacheRoot,
                ["ImageProcessing:HmacSecretKey"] = Convert.ToBase64String(HmacSecretKey),
                ["ConnectionStrings:umbracoDbDSN"] = dbConnectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            })));

        // Triggers the real Program.cs boot path (CreateUmbracoBuilder → BootUmbracoAsync → UseUmbraco)
        // synchronously, the same sequence a real `dotnet run` goes through — just against TestServer's
        // in-memory transport instead of a real Kestrel socket.
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        TryDeleteDirectory(MediaRoot);
        TryDeleteDirectory(CacheRoot);
        TryDeleteDirectory(Path.GetDirectoryName(_dbPath));
    }

    private static void TryDeleteDirectory(string? path)
    {
        try
        {
            if (path is not null && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort only — a leftover temp directory doesn't fail the test.
        }
    }
}
