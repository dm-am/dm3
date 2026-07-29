using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Configuration the core module and everything built on it needs.
/// </summary>
/// <remarks>
/// Registering <see cref="CoreModule"/> and binding the options it reads is one
/// call, so a host cannot do the first without the second. Copied into each
/// host by hand it drifted twice: once into a search client that three hosts
/// left unbound, and again into a notification worker that registers the whole
/// account and persistence stack while binding neither.
///
/// Some of these types are declared in DM.Domain.Core, which has no package
/// references at all on purpose. Binding lives here, on the first layer that is
/// allowed to know about IConfiguration.
/// </remarks>
public static class CoreConfigurationExtensions
{
    /// <summary>
    /// Binds the options every host shares, and refuses to start without the
    /// values that have no working default.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the sections from.</param>
    public static IServiceCollection AddDmCoreConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ConnectionStrings>()
            .Bind(configuration.GetSection(nameof(ConnectionStrings)))
            .Validate(
                cs => !string.IsNullOrEmpty(cs.Rdb) && !string.IsNullOrEmpty(cs.Mongo),
                "ConnectionStrings:Rdb and ConnectionStrings:Mongo are required")
            .ValidateOnStart();

        // WebUrl is what every generated link is built from: activation mails,
        // password resets, notification bodies. Empty, it produces links that
        // point at nothing, and it does so silently.
        services.AddOptions<IntegrationSettings>()
            .Bind(configuration.GetSection(nameof(IntegrationSettings)))
            .Validate(
                s => !string.IsNullOrEmpty(s.WebUrl),
                "IntegrationSettings:WebUrl is required")
            .ValidateOnStart();

        services.Configure<CdnConfiguration>(
            configuration.GetSection(nameof(CdnConfiguration)).Bind);
        services.Configure<ImageProxyConfiguration>(
            configuration.GetSection(nameof(ImageProxyConfiguration)).Bind);
        services.Configure<MirrorConfiguration>(
            configuration.GetSection(nameof(MirrorConfiguration)).Bind);
        services.Configure<BotConfiguration>(
            configuration.GetSection(nameof(BotConfiguration)).Bind);

        return services;
    }
}
