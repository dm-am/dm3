using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Web application builder extensions
/// </summary>
public static class WebBuilderExtensions
{
    private const int DefaultPort = 5000;

    /// <summary>
    /// Configures the web host with default gRPC settings
    /// </summary>
    public static IWebHostBuilder UseDefaultGrpc<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseKestrel(options =>
        {
            options.AllowSynchronousIO = true;
            options.ListenAnyIP(DefaultPort, cfg => cfg.Protocols = HttpProtocols.Http2);
        });

    /// <summary>
    /// Configures the web host with default HTTP settings
    /// </summary>
    public static IWebHostBuilder UseDefault<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseUrls($"http://+:{DefaultPort}");
}