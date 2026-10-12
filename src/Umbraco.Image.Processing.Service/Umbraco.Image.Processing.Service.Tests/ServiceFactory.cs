using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// Boots the service with the given settings on top of its <c>appsettings.json</c>. They go in as host
/// settings, which <see cref="WebApplicationFactory{TEntryPoint}" /> hands to the entry point as command-line
/// arguments, so <c>Program.cs</c> sees them where it reads configuration before <c>Build()</c> (the
/// processor, the sources) just as it would see a deployment's environment. <c>ConfigureAppConfiguration</c>
/// would only apply once the host is being built, after those reads.
/// </summary>
internal static class ServiceFactory
{
    public static WebApplicationFactory<Program> Create(IReadOnlyDictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach ((string key, string? value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    /// <summary>As <see cref="Create" />, but with an empty content root so <c>appsettings.json</c> contributes nothing.</summary>
    public static WebApplicationFactory<Program> CreateWithoutAppSettings(IReadOnlyDictionary<string, string?> settings)
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), "service-tests-empty-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        return Create(new Dictionary<string, string?>(settings) { [WebHostDefaults.ContentRootKey] = contentRoot });
    }
}
