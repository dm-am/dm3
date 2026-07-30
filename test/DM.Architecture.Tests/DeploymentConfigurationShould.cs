using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The deployment has to supply what the code refuses to assume.
/// </summary>
/// <remarks>
/// Some settings are written to fail safe rather than fail loud, and a safe
/// failure is silent by construction. Reverse-proxy support is the example this
/// class exists for: with no trusted network configured the API processes no
/// forwarded header at all, which is the right default for a host with no proxy
/// and the wrong one for the only stack that gets deployed. Nothing warned,
/// nothing failed a test, and every request looked like it came from nginx — one
/// address for the whole site, shared by the rate limiter and the login journal.
///
/// The code side is covered by ReverseProxySupportShould. What is asserted here is
/// the half no unit test can see: that the compose files agree with each other.
/// </remarks>
public class DeploymentConfigurationShould
{
    private const string BaseCompose = "docker-compose.yml";
    private const string PreviewCompose = "docker-compose.preview.yml";

    /// <summary>
    /// Walks up from the test binary to the repository root. The compose files are
    /// not copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string DockerDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docker")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return Path.Combine(directory!.FullName, "docker");
        }
    }

    private static string Read(string fileName)
    {
        var path = Path.Combine(DockerDirectory, fileName);
        File.Exists(path).Should().BeTrue($"{fileName} must exist at {path}");
        return File.ReadAllText(path);
    }

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

    /// <summary>The first <c>subnet:</c> value, quoted or bare.</summary>
    private static string? FindSubnet(string compose) =>
        FindValue(compose, "subnet:");

    private static string? FindTrustedNetwork(string compose) =>
        FindValue(compose, "DM_ReverseProxyConfiguration__TrustedNetworks__0:");

    private static string? FindValue(string compose, string key) => compose
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'))
        .Select(line =>
        {
            var marker = line.IndexOf(key, StringComparison.Ordinal);
            return marker < 0 ? null : line[(marker + key.Length)..].Trim().Trim('\'', '"', '-', ' ');
        })
        .FirstOrDefault(value => !string.IsNullOrEmpty(value));
}
