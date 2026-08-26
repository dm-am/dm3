using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Reverse proxy support registration
/// </summary>
public static class ReverseProxyExtensions
{
    /// <summary>
    /// Configures X-Forwarded-* processing so that forwarded headers are honoured
    /// for the configured proxies only
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddReverseProxySupport(
        this IServiceCollection services, IConfiguration configuration)
    {
        var proxyConfiguration = configuration
            .GetSection(nameof(ReverseProxyConfiguration))
            .Get<ReverseProxyConfiguration>() ?? new ReverseProxyConfiguration();

        var trustedNetworks = proxyConfiguration.TrustedNetworks.Select(ParseNetwork).ToArray();

        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            // An empty known-peer list makes the middleware skip the peer check
            // altogether and believe every caller, so "no proxy configured" has to
            // mean "process no forwarded header at all" — not an empty list.
            options.ForwardedHeaders = trustedNetworks.Length == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // nginx appends the peer to whatever the caller sent, so the trustworthy
            // entry is the rightmost one. ForwardLimit is how many entries the
            // middleware pops off that end — one per proxy in front of us.
            options.ForwardLimit = proxyConfiguration.ProxyCount;

            // The framework defaults trust loopback. Only the deployment knows where
            // its proxy actually lives, so nothing is trusted implicitly.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in trustedNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
    }

    private static System.Net.IPNetwork ParseNetwork(string value)
    {
        if (!System.Net.IPNetwork.TryParse(value, out var network))
        {
            throw new InvalidOperationException(
                $"{nameof(ReverseProxyConfiguration)}:{nameof(ReverseProxyConfiguration.TrustedNetworks)} " +
                $"contains \"{value}\", which is not a canonical CIDR network. " +
                "The address part has to be the network itself, host bits cleared: " +
                "172.28.0.0/16, not 172.28.0.1/16");
        }

        return network;
    }
}
