using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Umbraco.Image.Processing.Service;

/// <summary>
/// Optional telemetry: OpenTelemetry traces, metrics and logs, exported only to the destinations a deployment
/// configures. One that configures neither runs exactly as before, with no OpenTelemetry registered at all.
/// </summary>
/// <remarks>
/// Uses the standard OpenTelemetry and Azure Monitor settings, so a host sets them the way it sets them for any
/// other .NET service:
/// <list type="bullet">
/// <item><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> exports over OTLP: an Aspire dashboard, a collector, Azure Container
/// Apps' managed OpenTelemetry agent.</item>
/// <item><c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> exports to Azure Monitor Application Insights, Live Metrics
/// and Azure SDK (Blob) dependencies included.</item>
/// <item><c>OTEL_SERVICE_NAME</c> names the service, which Application Insights shows as its cloud role name.</item>
/// </list>
/// Both exporters may be set at once.
/// </remarks>
internal static class Telemetry
{
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public const string ApplicationInsightsConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private const string ServiceVersionAttribute = "service.version";

    private static readonly string ServiceVersion =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    public static WebApplicationBuilder AddTelemetry(this WebApplicationBuilder builder)
    {
        bool otlp = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]);
        string? connectionString = builder.Configuration[ApplicationInsightsConnectionStringKey];
        bool azureMonitor = !string.IsNullOrWhiteSpace(connectionString);
        if (!otlp && !azureMonitor)
        {
            return builder;
        }

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        OpenTelemetryBuilder openTelemetry = builder.Services.AddOpenTelemetry()
            // The build's informational version, which Application Insights shows as application_Version, so failures
            // and latency split by release.
            .ConfigureResource(resource => resource.AddAttributes([new(ServiceVersionAttribute, ServiceVersion)]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(HealthStatusPublisher.MeterName))
            .WithTracing(tracing => tracing
                // Probes are not traffic: a global load balancer alone probes from every point of presence. The
                // endpoints keep themselves out of the request metric (Health.cs).
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments(HealthEndpoints.Prefix))
                .AddHttpClientInstrumentation());

        if (otlp)
        {
            openTelemetry.UseOtlpExporter();
        }

        if (azureMonitor)
        {
            openTelemetry.UseAzureMonitor(options => options.ConnectionString = connectionString);
        }

        return builder;
    }
}
