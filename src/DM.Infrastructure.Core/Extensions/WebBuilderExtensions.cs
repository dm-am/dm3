using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Web application builder extensions
/// </summary>
public static class WebBuilderExtensions
{
    private const int DefaultPort = 5000;

    /// <summary>
    /// Maps what every worker host exposes besides its own work: the health probe
    /// the orchestrator reads and the metrics endpoint Prometheus scrapes
    /// </summary>
    /// <param name="builder">Application builder</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseDmWorkerEndpoints(this IApplicationBuilder builder) => builder
        .UseRouting()
        .UseHealthChecks("/_health")
        .UseEndpoints(route => route.MapPrometheusScrapingEndpoint("/metrics"));

    /// <summary>
    /// Configures the web host with default HTTP settings
    /// </summary>
    public static IWebHostBuilder UseDefault<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseUrls($"http://+:{DefaultPort}");
}
