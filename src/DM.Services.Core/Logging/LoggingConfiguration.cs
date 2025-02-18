using DM.Services.Core.Configuration;
using Jamq.Client.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.OpenSearch;
using System;

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
        string applicationName, IConfiguration configuration)
    {
        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", "Test")
            .WriteTo.Logger(lc => lc
                .Filter.ByExcluding(Matching.FromSource("Microsoft"))
                .WriteTo.OpenSearch(
                    new OpenSearchSinkOptions(new Uri(connectionStrings.Logs))
                    {
                        IndexFormat = "dm_logstash-{0:yyyy.MM.dd}",
                        InlineFields = true,
                        TypeName = null,
                    }))
            .WriteTo.Logger(lc => lc
                .WriteTo.Console())
            .CreateLogger();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .ConfigureResource(r => r.AddService(applicationName))
                .AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = true)
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources") // MongoDb is not too fancy
                .AddJamqClientInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(options => options.Endpoint = new Uri(connectionStrings.TracingEndpoint)));

        return services.AddLogging(b => b.AddSerilog());
    }
}