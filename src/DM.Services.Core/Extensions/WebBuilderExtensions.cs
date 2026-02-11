using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DM.Services.Core.Extensions;

/// <summary>
/// Расширения построителя веб-приложения
/// </summary>
public static class WebBuilderExtensions
{
    private const int DefaultPort = 5000;

    /// <summary>
    /// Настроить grpc-сервер по-умолчанию
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
    /// Настроить веб-сервер по-умолчанию
    /// </summary>
    public static IWebHostBuilder UseDefault<TStartup>(this IWebHostBuilder builder)
        where TStartup : class => builder
        .UseStartup<TStartup>()
        .UseUrls($"http://+:{DefaultPort}");
}