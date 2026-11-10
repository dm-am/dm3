using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// With no trusted network the API reads no forwarded header, a silent failure. The code side
/// is ReverseProxySupportShould; here the compose files agree with each other.
/// </remarks>
public class ApplicationNetworkShould
{
    /// <summary>
    /// The subnet is pinned rather than handed out by Docker, because it is the
    /// value the API trusts. A generated one cannot be named in configuration.
    /// </summary>
    [Fact]
    public void PinTheApplicationNetworkSubnet()
    {
        var subnet = FindSubnet(Read(BaseCompose));

        subnet.Should().NotBeNull("the dm-full-app network must declare an explicit subnet");
    }

    [Fact]
    public void TrustTheApplicationNetworkWhereNginxFrontsTheApi()
    {
        var subnet = FindSubnet(Read(BaseCompose));
        var preview = Read(PreviewCompose);

        preview.Should().Contain("nginx",
            "this assertion belongs to the overlay that puts a proxy in front of the API");

        preview.Should().Contain("DM_ReverseProxyConfiguration__TrustedNetworks__0",
            "without a trusted network the API processes no forwarded header, so the " +
            "rate limiter and the login journal see the proxy's address for everyone");
        preview.Should().Contain("DM_ReverseProxyConfiguration__ProxyCount",
            "the number of proxies decides how many entries are popped off the right " +
            "of X-Forwarded-For");

        FindTrustedNetwork(preview).Should().Be(subnet,
            "trusting a network other than the one the proxy runs on trusts nobody, " +
            "and trusting a wider one trusts more than our own containers");
    }

    /// <remarks>
    /// No stack of CI runs the overlay. An edge counted and not trusted stops the walk on itself,
    /// one trusted and not counted hands the API the address of the point of presence.
    /// </remarks>
    [Fact]
    public void CountAndTrustThePointOfPresenceWhereItFrontsTheMainServer()
    {
        var preview = Read(PreviewCompose);
        var origin = Read(PopOriginCompose);

        var proxies = FindValue(preview, "DM_ReverseProxyConfiguration__ProxyCount:");
        proxies.Should().NotBeNull("the preview overlay counts the edge in front of the API");
        var trusted = TrustedNetworkKey.Matches(preview).Count;
        trusted.Should().BeGreaterThan(0, "the preview overlay trusts the network of its edge");

        FindValue(origin, "DM_ReverseProxyConfiguration__ProxyCount:").Should().Be(
            (int.Parse(proxies!, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture),
            "the point of presence is one edge more in front of the one the preview overlay counts");
        FindValue(origin, $"DM_ReverseProxyConfiguration__TrustedNetworks__{trusted}:").Should().StartWith(
            "${POP_ADDRESS:?",
            "the edge counted next is trusted next, by the address the operator has to give");
        TrustedNetworkKey.Matches(origin).Count.Should().Be(1,
            "the overlay adds the point of presence to what the preview overlay trusts and replaces none of it");
    }

    private const string PopOriginCompose = "docker-compose.pop-origin.yml";

    private static readonly Regex TrustedNetworkKey =
        new(@"DM_ReverseProxyConfiguration__TrustedNetworks__\d+:", RegexOptions.Compiled);

    /// <summary>
    /// A published port that names no address listens on every interface, past the firewall rules
    /// of the install script. Only the overlay publishes one to the world.
    /// </summary>
    [Fact]
    public void PublishNoPortToTheWorldFromTheBaseStack()
    {
        var published = PublishedPorts(Read(BaseCompose));

        published.Should().NotBeEmpty("the parser must find the port lines");
        published.Should().OnlyContain(p => p.StartsWith("127.0.0.1:", StringComparison.Ordinal),
            "a publication with no address is reachable from outside and skips the firewall");
    }

    private static string[] PublishedPorts(string compose)
    {
        var ports = new List<string>();
        var inPorts = false;
        foreach (var raw in compose.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#')) continue;
            if (line == "ports:") { inPorts = true; continue; }
            if (inPorts && line.StartsWith("- ", StringComparison.Ordinal))
            {
                ports.Add(line[2..].Trim().Trim('\'', '"'));
                continue;
            }

            if (line.Length > 0) inPorts = false;
        }

        return ports.ToArray();
    }

    private static string? FindSubnet(string compose) =>
        FindValue(compose, "subnet:");

    private static string? FindTrustedNetwork(string compose) =>
        FindValue(compose, "DM_ReverseProxyConfiguration__TrustedNetworks__0:");
}
