using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Testing;
using DM.Web.API.Shared.Configuration;
using DM.Web.API.Shared.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Web.API.Tests.Shared;

public class ReverseProxySupportShould : UnitTestBase
{
    private const string ProxyAddress = "172.20.0.7";
    private const string ClientAddress = "203.0.113.9";
    private const string ForgedAddress = "8.8.8.8";

    [Fact]
    public async Task IgnoreForwardedAddressFromUntrustedPeer()
    {
        var context = CreateContext(peerAddress: ClientAddress, forwardedFor: ForgedAddress);

        await Handle(context, "172.16.0.0/12");

        context.GetClientAddress().Should().Be(ClientAddress);
    }

    [Fact]
    public async Task TakeForwardedAddressAppendedByTrustedProxy()
    {
        var context = CreateContext(peerAddress: ProxyAddress, forwardedFor: ClientAddress);

        await Handle(context, "172.16.0.0/12");

        context.GetClientAddress().Should().Be(ClientAddress);
    }

    /// <summary>
    /// nginx appends the peer to the header the caller sent, so a caller claiming
    /// to be 8.8.8.8 arrives as "8.8.8.8, &lt;real address&gt;". Only the entry the
    /// proxy itself wrote may be believed.
    /// </summary>
    [Fact]
    public async Task PreferProxyAppendedAddressOverTheOneTheCallerClaimed()
    {
        var context = CreateContext(peerAddress: ProxyAddress,
            forwardedFor: $"{ForgedAddress}, {ClientAddress}");

        await Handle(context, "172.16.0.0/12");

        context.GetClientAddress().Should().Be(ClientAddress);
    }

    [Fact]
    public async Task IgnoreForwardedAddressWhenNoProxyIsConfigured()
    {
        var context = CreateContext(peerAddress: ProxyAddress, forwardedFor: ForgedAddress);

        await Handle(context);

        context.GetClientAddress().Should().Be(ProxyAddress);
    }

    [Fact]
    public async Task ReportAnIPv4PeerOfADualStackSocketInIPv4Notation()
    {
        var context = CreateContext(peerAddress: $"::ffff:{ClientAddress}", forwardedFor: ForgedAddress);

        await Handle(context);

        context.GetClientAddress().Should().Be(ClientAddress);
    }

    [Fact]
    public void RejectMalformedTrustedNetwork()
    {
        var configure = () => BuildOptions("172.16.0.0");

        configure.Should().Throw<InvalidOperationException>()
            .WithMessage("*172.16.0.0*");
    }

    private static async Task Handle(HttpContext context, params string[] trustedNetworks)
    {
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, BuildOptions(trustedNetworks));
        await middleware.Invoke(context);
    }

    private static IOptions<ForwardedHeadersOptions> BuildOptions(params string[] trustedNetworks)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{nameof(ReverseProxyConfiguration)}:{nameof(ReverseProxyConfiguration.ProxyCount)}"] = "1"
        };
        for (var i = 0; i < trustedNetworks.Length; i++)
        {
            var key = $"{nameof(ReverseProxyConfiguration)}:" +
                $"{nameof(ReverseProxyConfiguration.TrustedNetworks)}:{i}";
            settings[key] = trustedNetworks[i];
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddReverseProxySupport(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>();
    }

    private static DefaultHttpContext CreateContext(string peerAddress, string forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peerAddress);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }
}
