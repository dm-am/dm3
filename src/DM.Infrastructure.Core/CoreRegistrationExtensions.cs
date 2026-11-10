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
    public static IServiceCollection AddDmCore(this IServiceCollection services)
    {
        // The module ships an ICache over IMemoryCache, so the cache is its own.
        services.AddMemoryCache();

        services.TryAddScoped<IIntentionManager, IntentionManager>();

        // The setter and the provider are one object per scope.
        services.TryAddScoped<CorrelationTokenProvider>();
        services.TryAddScoped<ICorrelationTokenProvider>(
            provider => provider.GetRequiredService<CorrelationTokenProvider>());
        services.TryAddScoped<ICorrelationTokenSetter>(
            provider => provider.GetRequiredService<CorrelationTokenProvider>());

        services.TryAddSingleton<ICursorService, CursorService>();

        // One object for the refresh that replaces the table and the letter that reads it.
        services.TryAddSingleton<IpCountryLookup>();
        services.TryAddSingleton<IIpCountryLookup>(
            provider => provider.GetRequiredService<IpCountryLookup>());

        services.TryAddSingleton<IAmazonS3>(provider =>
        {
            var amazonS3ClientProviders = provider.GetServices<IAmazonS3ClientProvider>();
            var selected = amazonS3ClientProviders.FirstOrDefault(p => p.CanBeUsed());
            if (selected is null)
            {
                // An unset CdnConfiguration:Provider lands here; the message names the setting.
                var configured = provider.GetRequiredService<IOptions<CdnConfiguration>>().Value.Provider;
                throw new InvalidOperationException(
                    $"No S3 client provider handles CdnConfiguration:Provider = {configured}.");
            }

            return selected.GetClient();
        });

        services.TryAddScoped<IImageProcessingService, ImageProcessingService>();

        services.TryAddSingleton<PublicIdService>();

        services.AddDefaultTypes(typeof(CoreRegistrationExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Keeps the country table of the sign-in letter fresh: downloads it when the
    /// configured file is missing and then once a week, and puts each checked copy
    /// in use without a restart.
    /// </summary>
    /// <remarks>
    /// For the host that sends the letter. Does nothing unless both
    /// GeoLocationConfiguration:CountryDatabasePath and CountryDatabaseUrl are set.
    /// </remarks>
    /// <param name="services">Service collection that already holds <see cref="AddDmCore"/>.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmCountryTableRefresh(this IServiceCollection services)
    {
        services.AddHostedService(provider =>
        {
            // Owned and disposed by the refresher. A weekly request opens a new
            // connection, so the one client pins no stale address.
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
