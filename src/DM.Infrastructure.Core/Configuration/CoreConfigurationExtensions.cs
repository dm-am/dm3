using System;
using System.Linq;
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
        // Bound, not required: the mail worker reads a queue and speaks SMTP and
        // touches neither store, yet this single call made it refuse to start without
        // both connection strings. A host declares what it actually opens through the
        // Require* calls below, so the demand stands next to the dependency.
        services.AddOptions<ConnectionStrings>()
            .Bind(configuration.GetSection(nameof(ConnectionStrings)));

        // Bound, not required, for the same reason as the connection strings above:
        // this is the call every host makes, and the mail worker builds no link at
        // all - the three senders that do live in DM.Domain.Account, an assembly it
        // does not scan. Demanding PublicUrl here made the worker refuse to start over
        // a value it never reads, which is the exact shape of what the Require*
        // split was introduced to end. RequireGeneratedLinks() is next door.
        services.AddOptions<SiteAddressConfiguration>()
            .Bind(configuration.GetSection(nameof(SiteAddressConfiguration)));

        services.Configure<CdnConfiguration>(
            configuration.GetSection(nameof(CdnConfiguration)).Bind);
        services.Configure<BotConfiguration>(
            configuration.GetSection(nameof(BotConfiguration)).Bind);

        // Two failures binding alone does not catch, both of them silent. A value that
        // is not hex throws FormatException the first time a page with a thumbnail
        // resolves the URL builder, which is a 500 on the first avatar rather than a
        // refusal to start. A key with no salt builds /insecure/ URLs that an imgproxy
        // holding a key answers 403 to, and no log names the reason.
        services.AddOptions<ImageProxyConfiguration>()
            .Bind(configuration.GetSection(nameof(ImageProxyConfiguration)))
            .Validate(IsUsableSigningPair,
                "ImageProxyConfiguration:Key and ImageProxyConfiguration:Salt must be " +
                "either both empty (unsigned local setup) or both hex-encoded. " +
                "Generate them with `openssl rand -hex 32`.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares that this host builds links back into the site.
    /// </summary>
    /// <remarks>
    /// PublicUrl is what every generated link is built from: activation mails,
    /// password resets, notification bodies. Empty, it produces links that point
    /// at nothing, and it does so silently — so a host that builds them says so
    /// and refuses to start without it. A host that builds none says nothing and
    /// starts.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection RequireGeneratedLinks(this IServiceCollection services)
    {
        services.AddOptions<SiteAddressConfiguration>()
            .Validate(
                s => !string.IsNullOrEmpty(s.PublicUrl),
                "SiteAddressConfiguration:PublicUrl is required")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Declares that this host opens the relational store.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection RequireRelationalStorage(this IServiceCollection services)
    {
        services.AddOptions<ConnectionStrings>()
            .Validate(cs => !string.IsNullOrEmpty(cs.Rdb), "ConnectionStrings:Rdb is required")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Declares that this host opens the document store.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection RequireDocumentStorage(this IServiceCollection services)
    {
        services.AddOptions<ConnectionStrings>()
            .Validate(cs => !string.IsNullOrEmpty(cs.Mongo), "ConnectionStrings:Mongo is required")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Declares that this host reads or writes object storage. None of these four
    /// values has a working default: without them every upload fails.
    /// </summary>
    /// <param name="services">Service collection.</param>
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

    private static bool IsUsableSigningPair(ImageProxyConfiguration imageProxy)
    {
        var hasKey = !string.IsNullOrEmpty(imageProxy.Key);
        var hasSalt = !string.IsNullOrEmpty(imageProxy.Salt);
        return (!hasKey && !hasSalt) || (hasKey && hasSalt && IsHex(imageProxy.Key) && IsHex(imageProxy.Salt));
    }

    private static bool IsHex(string value) =>
        value.Length % 2 == 0 && value.All(Uri.IsHexDigit);
}
