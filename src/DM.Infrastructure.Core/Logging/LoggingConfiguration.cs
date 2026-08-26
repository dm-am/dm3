using DM.Infrastructure.Core.Configuration;
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

/// <summary>
/// Configuration of logging
/// </summary>
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

        // The host decides what environment this is: it reads DOTNET_ and ASPNETCORE_
        // variables and the command line, so `--environment Development` sets it with
        // no variable existing at all. Reading the variable here made a second,
        // disagreeing answer inside one process - the pipeline mounting Swagger and
        // skipping HSTS while the log stayed at Information and shipped an
        // env=Production label to Loki.
        var environmentName = environment.EnvironmentName;
        var isDevelopment = environment.IsDevelopment();

        // A sink swallows its own failures by contract: Serilog will not let logging
        // throw into the code that logs. The Loki sink then buffers up to 50000 events
        // and retries for ten minutes before dropping them, and every step of that is
        // silent - a store that answers 400 to every push looks exactly like a quiet
        // application, and the troubleshooting guide sent the reader to the console for
        // an error the console had no way of carrying. This is the only channel the
        // library has for saying so. It writes per batch rather than per event, so a
        // sink that is down for a whole rotation window costs well under a megabyte.
        SelfLog.Enable(Console.Error);

        // One level rule governs both sinks. Debug on a server writes every framework
        // trace into a store that keeps a month of them, which costs disk and buries
        // the events worth reading; the framework noise is cut by source overrides rather
        // than by a filter attached to one sink and not the other. The volume of what
        // survives that rule is bounded by the two overrides below it.
        var configured = new LoggerConfiguration()
            .MinimumLevel.Is(MinimumLevel(configuration, isDevelopment))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            // A property and not a label: the commit changes on every deployment,
            // and a label that does starts a new stream each time. In the line it
            // costs nothing and answers the first question of every incident.
            .Enrich.WithProperty("Release", ReleaseInfo.Value)
            // Loki indexes labels, not message text, so only the dimensions a query
            // actually selects on are labels; everything else stays in the log line
            // and is filtered there. A label per correlation token or per user would
            // multiply streams without bound, which is the one way to make Loki slow.
            //
            // Two are declared here and a third arrives on its own: the sink adds
            // `level` in Grafana's vocabulary (error, warning, info) unless told not
            // to. It is worth having - severity is the one dimension every query
            // selects on - but it is not visible at this call, and a query written
            // from the list below alone will filter on a level that lives nowhere.
            .WriteTo.GrafanaLoki(
                connectionStrings.Logs,
                // Written by the sink out of the event, not by an enricher.
                //
                // An enricher put them on as ordinary properties named TraceId and
                // SpanId - and those two names are reserved by this sink, so it
                // renamed them to _TraceId and _SpanId in the body. The derived
                // field of the log datasource matches on a quote immediately before
                // the name, so it matched nothing at all: every line carried the
                // ids and not one of them was ever a link into a trace.
                //
                // In the body rather than as structured metadata for the same
                // reason: the datasource reads them with a regular expression over
                // the line.
                traceIdMode: LokiFieldDestination.Body,
                spanIdMode: LokiFieldDestination.Body,
                labels: [
                    new LokiLabel { Key = "app", Value = applicationName },
                    new LokiLabel { Key = "env", Value = environmentName },
                ],
                propertiesAsLabels: []);

        // The console copy has two readers, and one shape does not serve both.
        var format = ConsoleFormat(isDevelopment);
        Log.Logger = (format is null
                ? configured.WriteTo.Console()
                : configured.WriteTo.Console(format))
            .CreateLogger();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .ConfigureResource(r => r.AddService(applicationName))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // The successor package always records the parameterised SQL text:
                // upstream removed SetDbStatementForText after 1.12 (verified against
                // the 1.18.0-beta.1 options surface), so the per-environment switch the
                // predecessor had cannot be expressed. Parameter values are what carry
                // user data, and those stay off (SetDbQueryParameters defaults to
                // false). Residual exposure is literals EF inlines past parameters;
                // traces only ever reach our own Jaeger. An EnrichWithIDbCommand
                // workaround that strips the tag exists and is deliberately not taken.
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(DM.Infrastructure.Core.Tracing.DmActivitySource.Name)
                // The broker client's own instrumentation: RabbitMQ.Client 7.x
                // opens a publish span on one source and a deliver span on the
                // other, carries the context in the message headers, and parents
                // the deliver span to the publisher (the client default) - which
                // is what keeps the request and the work it caused one trace
                // across the broker. Subscribed to directly rather than through
                // RabbitMQ.Client.OpenTelemetry: that package is still a release
                // candidate, and the whole of what it adds over these two lines
                // is option plumbing this solution does not use. The names are
                // string literals because they are declared on
                // RabbitMQ.Client.RabbitMQActivitySource, and referencing the
                // broker client from this assembly for two constants would hand
                // every host a dependency only three of them have.
                .AddSource("RabbitMQ.Client.Publisher")
                .AddSource("RabbitMQ.Client.Subscriber")
                .AddOtlpExporter(options => options.Endpoint = new Uri(connectionStrings.TracingEndpoint)))
            .WithMetrics(builder => builder
                .ConfigureResource(r => r.AddService(applicationName))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // The client side of the connection pool: how many callers are
                // waiting for a connection, how many gave up waiting, and how close
                // the pool is to its ceiling. The exporter beside Postgres counts
                // sessions on the server and can answer none of those - it sees a
                // process that opened four connections, not an application whose
                // requests are queueing behind a full pool.
                .AddMeter("Npgsql")
                // Every measurement here is a defect: a question the authorization
                // layer could not answer and refused by default, which reads exactly
                // like a refusal the rules meant.
                .AddMeter(DM.Infrastructure.Core.Tracing.AuthorizationMetrics.MeterName)
                // Refusals at the front door, by the reason they were refused. The
                // request series answers 4xx for a typo and for somebody walking a
                // list of addresses, so without this the two are one number.
                .AddMeter(DM.Infrastructure.Core.Tracing.AuthenticationMetrics.MeterName)
                .AddMeter(DM.Infrastructure.Core.Tracing.UploadMetrics.MeterName)
                // Writes given up on after the work they belong to was committed
                // in the other store. Nobody is waiting on those: the request
                // succeeded, and the only symptom is one reader's badge being
                // wrong from then on.
                .AddMeter(DM.Infrastructure.Core.Tracing.StorageMetrics.MeterName)
                // A cache does not fail, it stops being useful, and both ends of
                // that cost: nothing hit is latency and memory spent for nothing,
                // everything hit is where a wrong entry is served the longest.
                .AddMeter(DM.Infrastructure.Core.Tracing.CacheMetrics.MeterName)
                // Without this the consumers export nothing about the messages they
                // handle, and a worker failing every one of them is indistinguishable
                // from an idle worker on every panel and every rule.
                .AddMeter(DM.Infrastructure.Core.Tracing.MessagingMetrics.MeterName)
                // Bucket boundaries, chosen rather than inherited.
                //
                // The SDK ships one ladder for every histogram in the process, and
                // its top bucket is ten seconds. Everything slower than that lands in
                // the overflow together, where a quantile can say nothing except "more
                // than ten" - which is every message that was retried even once, since
                // the retry ladder of the consumers alone outruns ten seconds. The
                // sizes have the opposite problem: measured in bytes against
                // boundaries meant for seconds, all three variants of every upload
                // fall into one bucket and the histogram answers nothing at all.
                //
                // Here rather than at the declaration because the overload that takes
                // the boundaries as advice arrived in .NET 9. When this moves to it,
                // they belong next to the unit, for the same reason the unit does.
                .AddView("dm.messaging.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300],
                })
                .AddView("dm.uploads.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
                })
                // The top boundary is the size the endpoint refuses above.
                .AddView("dm.uploads.input_size", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 10485760],
                })
                .AddView("dm.uploads.output_size", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 10485760],
                })
                .AddPrometheusExporter());

        return services.AddLogging(b => b.AddSerilog());
    }

    /// <summary>Configuration key the level is read from.</summary>
    private const string MinimumLevelKey = "Observability:MinimumLevel";

    /// <summary>
    /// Lowest level that reaches the sinks.
    /// </summary>
    /// <remarks>
    /// The environment picks the default and a deployment can override it. Without
    /// the override the only way to read Debug out of a running incident was to
    /// rebuild, or to set the environment to Development - which is not a logging
    /// switch at all: the same flag mounts Swagger, allows inline script and drops
    /// HSTS. There is deliberately no endpoint that changes this at runtime; the
    /// value is read once at startup, and a restart is the price of moving it.
    ///
    /// A value the enum does not know stops the process instead of falling back to
    /// the default. A silent fallback restores exactly the defect this replaces -
    /// a dial that turns and does nothing - and it does so at the moment somebody
    /// is turning it because a system is already broken.
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
    /// On a developer machine the console is read by a person, and the default
    /// template - timestamp, level, rendered message - is what makes it readable.
    /// On a server nobody reads it directly: it is the copy the container runtime
    /// keeps, and the only copy that survives the log store being down. That copy
    /// was written with the same template, so it carried the message and dropped
    /// every property on the event - which meant the fallback could not name the
    /// user, the correlation token or the trace of any line it kept.
    ///
    /// Rendered rather than plain compact JSON: both carry the trace and span from
    /// the event itself, so both work in the workers, but "@mt" leaves the reader
    /// to substitute the placeholders by hand where "@m" holds the finished text,
    /// the way the message does in the log store.
    ///
    /// The line grows severalfold, and the container runtime rotates that copy on
    /// size, so raising the rotation window is the matching move on a host that
    /// wants the same reach back in time.
    /// </remarks>
    internal static ITextFormatter? ConsoleFormat(bool isDevelopment) =>
        isDevelopment ? null : new RenderedCompactJsonFormatter();
}
