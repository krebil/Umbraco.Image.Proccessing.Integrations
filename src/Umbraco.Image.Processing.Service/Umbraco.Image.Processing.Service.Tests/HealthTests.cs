using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Umbraco.Image.Processing.Service.Tests;

/// <summary>
/// The three health endpoints answer in every environment, each runs only the checks its tag selects, and every
/// check's status reaches the health gauge.
/// </summary>
public sealed class HealthTests
{
    [Theory]
    [InlineData(HealthEndpoints.Live)]
    [InlineData(HealthEndpoints.Ready)]
    [InlineData(HealthEndpoints.Region)]
    public async Task Deployed_EachEndpointAnswersItsStatusAloneAndUncacheable(string path)
    {
        using WebApplicationFactory<Program> factory = Create();
        using HttpClient client = factory.CreateClient();

        foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Head })
        {
            using HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(method, path));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            if (method == HttpMethod.Get)
            {
                Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
            }
        }
    }

    [Theory]
    [InlineData(HealthTags.Ready, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HealthTags.Region, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable)]
    [InlineData("untagged", HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK)]
    public async Task AnUnhealthyCheck_FailsOnlyTheEndpointsItsTagSelects(string tag, HttpStatusCode live, HttpStatusCode ready, HttpStatusCode region)
    {
        using WebApplicationFactory<Program> factory = Create(services => services.AddHealthChecks()
            .AddCheck("failing", () => HealthCheckResult.Unhealthy("down"), [tag]));
        using HttpClient client = factory.CreateClient();

        Assert.Equal(live, (await client.GetAsync(HealthEndpoints.Live)).StatusCode);
        Assert.Equal(ready, (await client.GetAsync(HealthEndpoints.Ready)).StatusCode);
        Assert.Equal(region, (await client.GetAsync(HealthEndpoints.Region)).StatusCode);
    }

    [Fact]
    public async Task Development_TheEndpointsListTheirChecks()
    {
        using WebApplicationFactory<Program> factory = Create(environment: "Development");
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync(HealthEndpoints.Live);

        Assert.Contains("\"name\": \"self\"", body);
    }

    [Fact]
    public async Task Publisher_ReportsEveryCheckOnTheGauge()
    {
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var publisher = new HealthStatusPublisher(services.GetRequiredService<IMeterFactory>(), NullLogger<HealthStatusPublisher>.Instance);

        await publisher.PublishAsync(Report(("a", HealthStatus.Healthy), ("b", HealthStatus.Degraded), ("c", HealthStatus.Unhealthy)), default);

        Dictionary<string, int> observed = [];
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == HealthStatusPublisher.MeterName && instrument.Name == HealthStatusPublisher.StatusInstrument)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, tags, _) => observed[(string)tags[0].Value!] = value);
        listener.Start();
        listener.RecordObservableInstruments();

        Assert.Equal(new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 2 }, observed);
    }

    private static HealthReport Report(params (string Name, HealthStatus Status)[] entries) => new(
        entries.ToDictionary(e => e.Name, e => new HealthReportEntry(e.Status, null, TimeSpan.Zero, null, null)),
        TimeSpan.Zero);

    private static WebApplicationFactory<Program> Create(Action<IServiceCollection>? services = null, string environment = "Production") =>
        ServiceFactory.Create(new Dictionary<string, string?> { [WebHostDefaults.EnvironmentKey] = environment })
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(s => services?.Invoke(s)));
}
