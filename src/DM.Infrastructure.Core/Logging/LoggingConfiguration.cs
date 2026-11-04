using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Microsoft.Extensions.Hosting;
using Serilog.Sinks.Grafana.Loki;
using System;

namespace DM.Infrastructure.Core.Logging;

public static class LoggingConfiguration
{
    /// <summary>
    /// Register logger and add it to the service collection of the application
    /// </summary>
    public static IServiceCollection AddDmLogging(this IServiceCollection services,
        string applicationName, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);

        // A sink swallows its own failures; SelfLog is the only channel that says so.
        SelfLog.Enable(Console.Error);

        InstallLogger(configuration, applicationName, environment, connectionStrings.Logs);

        services.AddOpenTelemetry()
            .WithTracing(builder => Traces(builder, applicationName, connectionStrings.TracingEndpoint))
            .WithMetrics(builder => Metrics(builder, applicationName));

        return services.AddLogging(b => b.AddSerilog());
    }

    private static void InstallLogger(IConfiguration configuration, string applicationName,
        IHostEnvironment environment, string logsAddress)
    {
        // The host decides the environment, so the log never disagrees with the pipeline.
        var environmentName = environment.EnvironmentName;
        var isDevelopment = environment.IsDevelopment();

        // One level rule for both sinks; framework noise is cut by source overrides.
        var configured = new LoggerConfiguration()
            .MinimumLevel.Is(MinimumLevel(configuration, isDevelopment))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            // A property, not a label: a label that changes per deployment starts a new stream.
            .Enrich.WithProperty("Release", ReleaseInfo.Value)
            // Labels only for what a query selects on (docs/guides/MONITORING.md); the
            // sink adds `level` itself.
            .WriteTo.GrafanaLoki(
                logsAddress,
                // Written by the sink, in the body, where the log datasource finds them;
                // an enricher's TraceId is renamed by this sink and links nowhere.
                traceIdMode: LokiFieldDestination.Body,
                spanIdMode: LokiFieldDestination.Body,
                labels: [
                    new LokiLabel { Key = "app", Value = applicationName },
                    new LokiLabel { Key = "env", Value = environmentName },
                ],
                propertiesAsLabels: []);

        var format = ConsoleFormat(isDevelopment);
        Log.Logger = (format is null
                ? configured.WriteTo.Console()
                : configured.WriteTo.Console(format))
            .CreateLogger();
    }

    /// <summary>Configuration key the level is read from.</summary>
    private const string MinimumLevelKey = "Observability:MinimumLevel";

    /// <summary>
    /// Lowest level that reaches the sinks.
    /// </summary>
    /// <remarks>
    /// The environment picks the default, a deployment overrides it at startup. An
    /// unknown value stops the process: a silent fallback is a dial that does nothing.
    /// </remarks>
    private static LogEventLevel MinimumLevel(IConfiguration configuration, bool isDevelopment)
    {
        var configured = configuration[MinimumLevelKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information;
        }

        return Enum.TryParse<LogEventLevel>(configured, ignoreCase: true, out var level)
            ? level
            : throw new InvalidOperationException(
                $"{MinimumLevelKey} is set to '{configured}', which is not a log level. " +
                $"Expected one of: {string.Join(", ", Enum.GetNames<LogEventLevel>())}.");
    }

    /// <summary>
    /// Shape of the console copy, or <c>null</c> to keep the themed default template.
    /// </summary>
    /// <remarks>
    /// A person reads the console of a developer machine. On a server it is the copy
    /// that survives the log store being down, so it keeps every property, with the
    /// message rendered.
    /// </remarks>
    internal static ITextFormatter? ConsoleFormat(bool isDevelopment) =>
        isDevelopment ? null : new RenderedCompactJsonFormatter();

    private static TracerProviderBuilder Traces(TracerProviderBuilder builder, string applicationName,
        string tracingEndpoint) => builder
        .ConfigureResource(r => r.AddService(applicationName))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // Records the parameterised SQL text; parameter values, which carry user
        // data, stay off by default.
        // Literals EF inlines past parameters stay in the text: accepted, traces reach only our own Jaeger.
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource(DmActivitySource.Name)
        // The broker client's own sources, which keep one trace across the broker.
        // Literals, so that this assembly does not reference the broker client.
        .AddSource("RabbitMQ.Client.Publisher")
        .AddSource("RabbitMQ.Client.Subscriber")
        .AddOtlpExporter(options => options.Endpoint = new Uri(tracingEndpoint));

    private static MeterProviderBuilder Metrics(MeterProviderBuilder builder, string applicationName)
    {
        var meters = builder
            .ConfigureResource(r => r.AddService(applicationName))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            // The client side of the connection pool, which the Postgres exporter cannot see.
            .AddMeter("Npgsql")
            .AddMeter(AuthorizationMetrics.MeterName)
            .AddMeter(AuthenticationMetrics.MeterName)
            .AddMeter(UploadMetrics.MeterName)
            .AddMeter(StorageMetrics.MeterName)
            .AddMeter(CacheMetrics.MeterName)
            .AddMeter(MessagingMetrics.MeterName);

        return HistogramBuckets(meters).AddPrometheusExporter();
    }

    // The default ladder tops out at ten seconds, which a retried message outruns,
    // and means nothing for sizes in bytes.
    private static MeterProviderBuilder HistogramBuckets(MeterProviderBuilder builder) => builder
        .AddView("dm.messaging.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300],
        })
        .AddView("dm.uploads.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
        })
        .AddView("dm.uploads.input_size", new ExplicitBucketHistogramConfiguration { Boundaries = SizeBoundaries })
        .AddView("dm.uploads.output_size", new ExplicitBucketHistogramConfiguration { Boundaries = SizeBoundaries });

    /// <summary>Bytes; the top boundary is the size the upload endpoint refuses above.</summary>
    private static readonly double[] SizeBoundaries = [1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 10485760];
}
