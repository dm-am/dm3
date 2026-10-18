using System;
using System.Linq;
using System.Net.Http;
using Amazon.S3;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Authorization;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Correlation;
using DM.Domain.Core.Geo;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Geo;
using DM.Infrastructure.Core.Paging;
using DM.Infrastructure.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core;

/// <summary>
/// Services of the infrastructure core module.
/// </summary>
public static class CoreRegistrationExtensions
{
    /// <summary>
    /// Registers the core infrastructure services and the assembly's default types.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmCore(this IServiceCollection services)
    {
        // The module ships an ICache over IMemoryCache, so the backing cache is
        // part of the module rather than a line each host remembers to add.
        // Idempotent: the framework TryAdds its registrations.
        services.AddMemoryCache();

        services.TryAddScoped<IIntentionManager, IntentionManager>();

        // One token per scope, and the setter and the provider have to be the
        // same object within it - forwarding factories, not two registrations.
        services.TryAddScoped<CorrelationTokenProvider>();
        services.TryAddScoped<ICorrelationTokenProvider>(
            provider => provider.GetRequiredService<CorrelationTokenProvider>());
        services.TryAddScoped<ICorrelationTokenSetter>(
            provider => provider.GetRequiredService<CorrelationTokenProvider>());

        services.TryAddSingleton<ICursorService, CursorService>();

        // Singleton: the table it reads is tens of megabytes, and it is read
        // once per copy, not once per question. Registered as itself and
        // forwarded, so the refresh that replaces the table and the letter that
        // reads it hold the same object; the scan would otherwise register the
        // class as itself, transient, and the refresh would fill a copy nobody asks.
        services.TryAddSingleton<IpCountryLookup>();
        services.TryAddSingleton<IIpCountryLookup>(
            provider => provider.GetRequiredService<IpCountryLookup>());

        // S3 Client registration (uses IAmazonS3ClientProvider implementations)
        services.TryAddSingleton<IAmazonS3>(provider =>
        {
            var amazonS3ClientProviders = provider.GetServices<IAmazonS3ClientProvider>();
            var selected = amazonS3ClientProviders.FirstOrDefault(p => p.CanBeUsed());
            if (selected is null)
            {
                // S3Provider defaults to a member no implementation claims, so a host
                // that leaves CdnConfiguration:Provider unset lands here. First() used
                // to report that as "Sequence contains no matching element", which
                // names neither the setting nor the value it holds.
                var configured = provider.GetRequiredService<IOptions<CdnConfiguration>>().Value.Provider;
                throw new InvalidOperationException(
                    $"No S3 client provider handles CdnConfiguration:Provider = {configured}.");
            }

            return selected.GetClient();
        });

        // The single avatar image processing service: resize+crop
        // into Medium/Small WebP thumbnails, EXIF strip of the original, decompression-
        // bomb protection, magic-byte content-type detection, atomic
        // S3 batch upload with compensating cleanup on failure.
        services.TryAddScoped<IImageProcessingService, ImageProcessingService>();

        // GC of obsolete uploads (UploadGarbageCollector) lives in
        // Infrastructure.Persistence (needs DmDbContext) and is registered
        // there via AddDmPersistence.

        // PublicId encoding/decoding service (stateless, singleton)
        services.TryAddSingleton<IPublicIdService, PublicIdService>();

        services.AddDefaultTypes(typeof(CoreRegistrationExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Keeps the country table of the sign-in letter fresh: downloads it when the
    /// configured file is missing and then once a week, and puts each checked copy
    /// in use without a restart.
    /// </summary>
    /// <remarks>
    /// Called by the host that sends the letter and by no other: a host that
    /// never asks for a country has no reason to fetch the table. Does nothing
    /// unless both GeoLocationConfiguration:CountryDatabasePath and
    /// GeoLocationConfiguration:CountryDatabaseUrl are set.
    /// </remarks>
    /// <param name="services">Service collection that already holds <see cref="AddDmCore"/>.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmCountryTableRefresh(this IServiceCollection services)
    {
        services.AddHostedService(provider =>
        {
            // Owned by the refresher, which disposes it with itself. One client
            // for the life of the process; a weekly request opens a new connection
            // each time, so a long-lived client does not pin a stale address.
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DM3-CountryTable/1.0");

            return new CountryTableRefresher(
                provider.GetRequiredService<IpCountryLookup>(),
                provider.GetRequiredService<IOptions<GeoLocationConfiguration>>(),
                provider.GetRequiredService<IDateTimeProvider>(),
                provider.GetRequiredService<ILogger<CountryTableRefresher>>(),
                http);
        });

        return services;
    }
}
