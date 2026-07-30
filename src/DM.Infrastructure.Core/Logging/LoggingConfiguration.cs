using DM.Infrastructure.Core.Configuration;
using Jamq.Client.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using System;

namespace DM.Infrastructure.Core.Logging;

/// <summary>
/// Configuration of logging
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Register logger and add it to the service collection of the application
    /// </summary>
    public static IServiceCollection AddDmLogging(this IServiceCollection services,
        string applicationName, IConfiguration configuration)
    {
        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isDevelopment = environmentName == "Development";

        // One level rule governs both sinks. Debug on a server writes every framework
        // trace into a store with no retention, which costs disk and buries the events
        // worth reading; the framework noise is cut by source overrides rather than by a
        // filter attached to one sink and not the other.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<ActivityEnricher>()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            // Loki indexes labels, not message text, so only the two dimensions a
            // query actually selects on are labels; everything else stays in the
            // log line and is filtered there. A label per correlation token or
            // per user would multiply streams without bound, which is the one way
            // to make Loki slow.
            .WriteTo.GrafanaLoki(
                connectionStrings.Logs,
                labels: [
                    new LokiLabel { Key = "app", Value = applicationName },
                    new LokiLabel { Key = "env", Value = environmentName },
                ],
                propertiesAsLabels: [])
            .WriteTo.Console()
            .CreateLogger();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .ConfigureResource(r => r.AddService(applicationName))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // SQL text carries the parameter values a query was built with, so it goes
                // into a trace only where the trace stays on the developer's machine.
                .AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = isDevelopment)
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources") // MongoDb is not too fancy
                .AddSource(DM.Infrastructure.Core.Tracing.DmActivitySource.Name)
                .AddJamqClientInstrumentation()
                .AddOtlpExporter(options => options.Endpoint = new Uri(connectionStrings.TracingEndpoint)))
            .WithMetrics(builder => builder
                .ConfigureResource(r => r.AddService(applicationName))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(DM.Infrastructure.Core.Tracing.UploadMetrics.MeterName)
                .AddPrometheusExporter());

        return services.AddLogging(b => b.AddSerilog());
    }
}