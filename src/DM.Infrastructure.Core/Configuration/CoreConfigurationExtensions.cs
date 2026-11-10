using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Configuration the core module and everything built on it needs.
/// </summary>
/// <remarks>
/// One call per host, so that no host registers the module without binding what it
/// reads. Here because DM.Domain.Core, which declares some of the types, knows no
/// IConfiguration.
/// </remarks>
public static class CoreConfigurationExtensions
{
    /// <summary>
    /// Binds the options every host shares, and refuses to start without the
    /// values that have no working default.
    /// </summary>
    public static IServiceCollection AddDmCoreConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Bound, not required: a host demands what it opens through the Require* calls.
        services.AddOptions<ConnectionStrings>()
            .Bind(configuration.GetSection(nameof(ConnectionStrings)));

        // Bound, not required: see RequireGeneratedLinks.
        services.AddOptions<SiteAddressConfiguration>()
            .Bind(configuration.GetSection(nameof(SiteAddressConfiguration)));

        services.AddOptions<CdnConfiguration>()
            .Bind(configuration.GetSection(nameof(CdnConfiguration)));
        // Bound, not required: left empty the lookup answers nothing.
        services.AddOptions<GeoLocationConfiguration>()
            .Bind(configuration.GetSection(nameof(GeoLocationConfiguration)));
        services.AddOptions<BotConfiguration>()
            .Bind(configuration.GetSection(nameof(BotConfiguration)));

        return services;
    }

    /// <summary>
    /// Declares that this host builds links back into the site.
    /// </summary>
    /// <remarks>
    /// Empty, PublicUrl silently builds links that point at nothing.
    /// </remarks>
    public static IServiceCollection RequireGeneratedLinks(this IServiceCollection services)
    {
        services.AddOptions<SiteAddressConfiguration>()
            .Validate(
                s => !string.IsNullOrEmpty(s.PublicUrl),
                "SiteAddressConfiguration:PublicUrl is required")
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection RequireRelationalStorage(this IServiceCollection services)
    {
        services.AddOptions<ConnectionStrings>()
            .Validate(cs => !string.IsNullOrEmpty(cs.Rdb), "ConnectionStrings:Rdb is required")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Declares that this host reads or writes object storage. None of these four
    /// values has a working default: without them every upload fails.
    /// </summary>
    public static IServiceCollection RequireObjectStorage(this IServiceCollection services)
    {
        services.AddOptions<CdnConfiguration>()
            .Validate(
                cdn => !string.IsNullOrEmpty(cdn.Url) && !string.IsNullOrEmpty(cdn.BucketName) &&
                       !string.IsNullOrEmpty(cdn.AccessKey) && !string.IsNullOrEmpty(cdn.SecretKey),
                "CdnConfiguration:Url, BucketName, AccessKey and SecretKey are required")
            .ValidateOnStart();
        return services;
    }
}
