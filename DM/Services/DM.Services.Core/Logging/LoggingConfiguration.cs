using System;
using DM.Services.Core.Configuration;
using Jamq.Client.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Filters;

namespace DM.Services.Core.Logging;

/// <summary>
/// Configuration of logging
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Register logger and add it to the service collection of the application
    /// </summary>
    public static IServiceCollection AddDmLogging(this IServiceCollection services,
        string applicationName, IConfigurationRoot configuration = null)
    {
        configuration ??= ConfigurationFactory.Default;

        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", "Test")
            .WriteTo.Logger(lc => lc
                .Filter.ByExcluding(Matching.FromSource("Microsoft"))
                .WriteTo.Elasticsearch(
                    connectionStrings.Logs,
                    "dm_logstash-{0:yyyy.MM.dd}",
                    inlineFields: true))
            .WriteTo.Logger(lc => lc
                .WriteTo.Console())
            .CreateLogger();

        services.AddOpenTelemetryTracing(builder => builder
            .ConfigureResource(r => r.AddService(applicationName))
            .AddAspNetCoreInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddHttpClientInstrumentation(options => options.Filter = message => 
                message.RequestUri?.ToString().Contains("_bulk") is not true)
            .AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = true)
            .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources") // MongoDb is not too fancy
            .AddJamqClientInstrumentation()
            .AddConsoleExporter()
            .AddJaegerExporter(options => options.Endpoint = new Uri(connectionStrings.JaegerTracingEndpoint)));

        return services.AddLogging(b => b.AddSerilog());
    }
}