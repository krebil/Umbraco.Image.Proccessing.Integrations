using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// Telemetry is opt-in: without an exporter configured the service registers no OpenTelemetry at all, and either
/// exporter setting on its own turns traces and metrics on.
/// </summary>
public sealed class TelemetryTests
{
    // A well-formed connection string whose endpoints refuse at once, so nothing leaves the machine.
    private const string UnreachableConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/;LiveEndpoint=http://127.0.0.1:9/";

    [Fact]
    public void WithoutAnExporter_RegistersNoOpenTelemetry()
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?>());
        _ = factory.CreateClient();

        Assert.Null(factory.Services.GetService<TracerProvider>());
        Assert.Null(factory.Services.GetService<MeterProvider>());
    }

    [Theory]
    [InlineData("APPLICATIONINSIGHTS_CONNECTION_STRING", UnreachableConnectionString)]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9")]
    public void EitherExporterSetting_RegistersTracesAndMetrics(string key, string value)
    {
        using WebApplicationFactory<Program> factory = ServiceFactory.Create(new Dictionary<string, string?> { [key] = value });
        _ = factory.CreateClient();

        Assert.NotNull(factory.Services.GetService<TracerProvider>());
        Assert.NotNull(factory.Services.GetService<MeterProvider>());
    }
}
