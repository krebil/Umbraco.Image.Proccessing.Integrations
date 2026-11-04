using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umbraco.Image.Processing.Service;

/// <summary>
/// The health endpoints the service maps in every environment. One prefix, so a telemetry filter, an auth exclusion
/// or an edge rule each need a single entry.
/// </summary>
public static class HealthEndpoints
{
    public const string Prefix = "/health";

    /// <summary>
    /// The process answers: <see cref="HealthTags.Live"/> checks only, never a dependency. Liveness and startup probes;
    /// a failure restarts the replica.
    /// </summary>
    public const string Live = Prefix + "/live";

    /// <summary>
    /// This instance can serve: <see cref="HealthTags.Ready"/> checks. A failure takes the replica out of rotation, never
    /// restarts it. The readiness probe, a global load balancer's origin probe (Front Door, Traffic Manager) and
    /// availability tests: a deployment whose replicas are all unready has nothing to route to, so the load balancer
    /// sends its traffic to another.
    /// </summary>
    public const string Ready = Prefix + "/ready";
}

/// <summary>
/// A check's tags decide which endpoint runs it, so pick the tag by what a restart or taking traffic away would fix. A
/// check with neither is still run and reported to telemetry (<see cref="HealthStatusPublisher"/>) but fails no probe:
/// the place for a dependency shared beyond the deployment, where draining every replica at once helps nobody.
/// </summary>
public static class HealthTags
{
    /// <summary>The process itself. Never a dependency, or a dependency outage restarts every replica.</summary>
    public const string Live = "live";

    /// <summary>
    /// What the instance cannot serve without: its own state, or a dependency of its deployment (its original-image
    /// storage). Taking it out of rotation helps; when every replica fails it, a load balancer fails the deployment over.
    /// </summary>
    public const string Ready = "ready";
}

/// <summary>Registers and maps the health checks (<see cref="HealthEndpoints"/>). Always on: probes need them deployed.</summary>
internal static class Health
{
    private const string HealthCheckServiceLogCategory = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";

    public static WebApplicationBuilder AddHealth(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [HealthTags.Live]);

        // Every check, tagged or not, reaches telemetry on this schedule. The framework defaults, written out because
        // anything alerting on the gauge is tuned to them.
        builder.Services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(5);
            options.Period = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<IHealthCheckPublisher, HealthStatusPublisher>();

        // The health check service logs an Error for every unhealthy result of every run (each probe, and each publisher
        // run on every replica) for as long as a check stays unhealthy. HealthStatusPublisher logs the change once instead.
        builder.Logging.AddFilter(HealthCheckServiceLogCategory, LogLevel.None);

        return builder;
    }

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        Map(HealthEndpoints.Live, check => check.Tags.Contains(HealthTags.Live));
        Map(HealthEndpoints.Ready, check => check.Tags.Contains(HealthTags.Ready));


        return app;

        void Map(string path, Func<HealthCheckRegistration, bool> predicate) => app
            .MapHealthChecks(path, new HealthCheckOptions
            {
                Predicate = predicate,

                // Deployed, the body is the status alone ("Healthy"); the per-check detail goes to telemetry. In
                // Development, the detail. 200 for Healthy and Degraded, 503 for Unhealthy, never cacheable (defaults).
                ResponseWriter = app.Environment.IsDevelopment() ? HealthResponseWriter.WriteDetailAsync : HealthResponseWriter.WriteStatusAsync,
            })
            // Ahead of the image pipeline, and out of the request metric (and, in Telemetry.cs, out of tracing), so
            // probe volume does not show up as traffic.
            .ShortCircuit()
            .DisableHttpMetrics();
    }
}

/// <summary>
/// Reports every registered check to telemetry on the health check service's schedule: the gauge
/// <see cref="StatusInstrument"/> per check (0 healthy, 1 degraded, 2 unhealthy; dimension <c>check</c>), and one log
/// line when a check's status changes.
/// </summary>
internal sealed partial class HealthStatusPublisher : IHealthCheckPublisher, IDisposable
{
    public const string MeterName = "Umbraco.Image.Processing.Health";
    public const string StatusInstrument = "health.check.status";

    private readonly ConcurrentDictionary<string, HealthStatus> _statuses = new(StringComparer.Ordinal);
    private readonly ILogger<HealthStatusPublisher> _logger;
    private readonly Meter _meter;

    public HealthStatusPublisher(IMeterFactory meterFactory, ILogger<HealthStatusPublisher> logger)
    {
        _logger = logger;
        _meter = meterFactory.Create(MeterName);
        _meter.CreateObservableGauge(StatusInstrument, Observe, description: "Health check status: 0 healthy, 1 degraded, 2 unhealthy.");
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach ((string name, HealthReportEntry entry) in report.Entries)
        {
            HealthStatus? previous = _statuses.TryGetValue(name, out HealthStatus known) ? known : null;
            _statuses[name] = entry.Status;

            // Unchanged is not news, and neither is the first report of a healthy check.
            if (previous == entry.Status || (previous is null && entry.Status == HealthStatus.Healthy))
            {
                continue;
            }

            if (entry.Status == HealthStatus.Healthy)
            {
                LogRecovered(_logger, name, previous);
            }
            else
            {
                LogChanged(_logger, entry.Exception, name, entry.Status, previous, entry.Description);
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose() => _meter.Dispose();

    private IEnumerable<Measurement<int>> Observe()
    {
        foreach ((string name, HealthStatus status) in _statuses)
        {
            yield return new Measurement<int>(Value(status), new KeyValuePair<string, object?>("check", name));
        }
    }

    private static int Value(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => 0,
        HealthStatus.Degraded => 1,
        _ => 2,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Health check {Check} is {Status} (was {Previous}): {Description}")]
    private static partial void LogChanged(ILogger logger, Exception? exception, string check, HealthStatus status, HealthStatus? previous, string? description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Health check {Check} is Healthy again (was {Previous})")]
    private static partial void LogRecovered(ILogger logger, string check, HealthStatus? previous);
}

/// <summary>The health endpoints' bodies.</summary>
internal static class HealthResponseWriter
{
    /// <summary>The overall status only: a probe reads the status code, and nothing about the checks leaves the process.</summary>
    public static Task WriteStatusAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }

    /// <summary>Development: every check the endpoint ran, with its description, duration and tags.</summary>
    public static async Task WriteDetailAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        await using var json = new Utf8JsonWriter(context.Response.Body, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("status", report.Status.ToString());
        json.WriteNumber("durationMs", Math.Round(report.TotalDuration.TotalMilliseconds));
        json.WriteStartArray("checks");
        foreach ((string name, HealthReportEntry entry) in report.Entries)
        {
            json.WriteStartObject();
            json.WriteString("name", name);
            json.WriteString("status", entry.Status.ToString());
            json.WriteNumber("durationMs", Math.Round(entry.Duration.TotalMilliseconds));
            json.WriteString("description", entry.Description ?? entry.Exception?.Message);
            json.WriteStartArray("tags");
            foreach (string tag in entry.Tags)
            {
                json.WriteStringValue(tag);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }
}
