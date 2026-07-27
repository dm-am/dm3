using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Web application builder extensions
/// </summary>
public static class WebBuilderExtensions
{
    private const int DefaultPort = 5000;

    /// <summary>
    /// Port serving the health check and the metrics scrape of a gRPC host
    /// </summary>
    public const int ManagementPort = 5100;

    /// <summary>
    /// Configures the web host with default gRPC settings
    /// </summary>
    public static IWebHostBuilder UseDefaultGrpc<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseKestrel(options =>
        {
            options.AllowSynchronousIO = true;

            // gRPC over cleartext requires an HTTP/2-only endpoint: without TLS
            // there is no ALPN to negotiate with, so Http1AndHttp2 would simply
            // serve HTTP/1.1 and every call would fail. Health probes and the
            // metrics scrape are HTTP/1.1 clients and get an endpoint of their own
            // instead — on the shared port they were answered with 400.
            options.ListenAnyIP(DefaultPort, cfg => cfg.Protocols = HttpProtocols.Http2);
            options.ListenAnyIP(ManagementPort, cfg => cfg.Protocols = HttpProtocols.Http1);
        });

    /// <summary>
    /// Maps what every worker host exposes besides its own work: the health probe
    /// the orchestrator reads and the metrics endpoint Prometheus scrapes
    /// </summary>
    /// <param name="builder">Application builder</param>
    /// <param name="configure">Endpoints of the worker itself</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseDmWorkerEndpoints(this IApplicationBuilder builder,
        Action<IEndpointRouteBuilder>? configure = null) => builder
        .UseRouting()
        .UseHealthChecks("/_health")
        .UseEndpoints(route =>
        {
            route.MapPrometheusScrapingEndpoint("/metrics");
            configure?.Invoke(route);
        });

    /// <summary>
    /// Configures the web host with default HTTP settings
    /// </summary>
    public static IWebHostBuilder UseDefault<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseUrls($"http://+:{DefaultPort}");
}