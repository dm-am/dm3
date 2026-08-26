using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Testing;
using DM.Web.API.Shared.Configuration;
using DM.Web.API.Shared.Http;
using AwesomeAssertions;
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

    /// <summary>
    /// The session cookie is marked Secure exactly when the request is https, and
    /// behind nginx this header is the only thing that says so: dropped from the
    /// flags, a stand on TLS looks like plain http to the API.
    /// </summary>
    [Fact]
    public async Task TakeForwardedSchemeAppendedByTrustedProxy()
    {
        var context = CreateContext(peerAddress: ProxyAddress, forwardedFor: ClientAddress);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await Handle(context, "172.16.0.0/12");

        context.Request.IsHttps.Should().BeTrue(
            "the cookie the login writes is Secure or not by this, and nothing else");
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

    private const string PointOfPresenceAddress = "198.51.100.4";

    /// <summary>
    /// A second edge in front of the first is a second hop, and both halves of that
    /// have to be written down.
    /// </summary>
    /// <remarks>
    /// Each proxy appends the peer it saw to the header, so behind two of them the
    /// visitor is two entries from the end. Left at one hop the application reads
    /// the address of the nearer edge instead — one address for every visitor, which
    /// makes the per-account rate limit a shared budget for the whole site and
    /// writes the same address into every line of the login journal.
    ///
    /// The trusted entry is the address of that second edge and nothing wider: a
    /// range covers whoever else lives in it, and the header they send is believed.
    /// </remarks>
    [Fact]
    public async Task ReadThroughEveryEdgeThatWasDeclared()
    {
        var context = CreateContext(peerAddress: ProxyAddress,
            forwardedFor: $"{ClientAddress}, {PointOfPresenceAddress}");

        await Handle(context, 2, "172.16.0.0/12", $"{PointOfPresenceAddress}/32");

        context.GetClientAddress().Should().Be(ClientAddress);
    }

    [Fact]
    public async Task StopAtTheEdgeNobodyDeclared()
    {
        var context = CreateContext(peerAddress: ProxyAddress,
            forwardedFor: $"{ClientAddress}, {PointOfPresenceAddress}");

        await Handle(context, 2, "172.16.0.0/12");

        context.GetClientAddress().Should().Be(PointOfPresenceAddress,
            "an edge that is not on the list is a caller like any other, and the entry " +
            "in front of it is whatever that caller chose to write");
    }

    [Fact]
    public async Task BelieveNoEntryBeyondTheEdgesThatWereDeclared()
    {
        var context = CreateContext(peerAddress: ProxyAddress,
            forwardedFor: $"{ForgedAddress}, {ClientAddress}, {PointOfPresenceAddress}");

        await Handle(context, 2, "172.16.0.0/12", $"{PointOfPresenceAddress}/32");

        context.GetClientAddress().Should().Be(ClientAddress,
            "the count of hops is the whole of the protection: the middleware never " +
            "reaches the entry the visitor wrote");
    }

    private static async Task Handle(HttpContext context, params string[] trustedNetworks) =>
        await Handle(context, 1, trustedNetworks);

    private static async Task Handle(HttpContext context, int proxyCount, params string[] trustedNetworks)
    {
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, BuildOptions(proxyCount, trustedNetworks));
        await middleware.Invoke(context);
    }

    private static IOptions<ForwardedHeadersOptions> BuildOptions(params string[] trustedNetworks) =>
        BuildOptions(1, trustedNetworks);

    private static IOptions<ForwardedHeadersOptions> BuildOptions(int proxyCount, params string[] trustedNetworks)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{nameof(ReverseProxyConfiguration)}:{nameof(ReverseProxyConfiguration.ProxyCount)}"] =
                proxyCount.ToString()
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
