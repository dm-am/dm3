using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// The second door runs an edge and a frontend, carries no secret of the main
/// server, and sends its API traffic across the network.
/// </summary>
/// <remarks>POINT_OF_PRESENCE.md describes the door; the shared edge is EdgeConfigurationShould's.</remarks>
public class PointOfPresenceShould
{
    /// <remarks>
    /// The door runs no application and must not carry the main server's passwords or the session key.
    /// The placeholders are required by the base compose anchor and spelled so no real value matches.
    /// </remarks>
    [Fact]
    public void GiveThePointOfPresenceATemplateWithNoRealSecretInIt()
    {
        var template = Path.Combine(DockerDirectory, ".env.pop.example");
        File.Exists(template).Should().BeTrue(
            "the guides copy this file, and without it they name the one that holds every password");

        var content = File.ReadAllText(template);
        var example = File.ReadAllText(Path.Combine(DockerDirectory, ".env.example"));

        foreach (var secret in new[]
                 {
                     "POSTGRES_PASSWORD", "RABBITMQ_DEFAULT_PASS", "GF_SECURITY_ADMIN_PASSWORD",
                 })
        {
            content.Should().NotContain($"{secret}=",
                $"{secret} belongs to the main server and has no use on a point of presence");
        }

        foreach (var required in new[]
                 {
                     "DM_CryptoConfiguration__KeyBase64",
                     "STORAGE_APP_PASSWORD", "STORAGE_BACKUP_PASSWORD",
                 })
        {
            var line = content.Split('\n').FirstOrDefault(l => l.StartsWith($"{required}=", StringComparison.Ordinal));
            line.Should().NotBeNull(
                $"{required} is declared through ${{...:?}} in the base file, so compose stops without it");
            line.Should().Contain("not-used-on-a-point-of-presence",
                "a placeholder has to be unmistakable: a real-looking value here would be " +
                "carried onto that machine and read as working");

            var real = example.Split('\n').FirstOrDefault(l => l.StartsWith($"{required}=", StringComparison.Ordinal));
            line.Should().NotBe(real, $"{required} must not be the value the repository publishes");
        }

        foreach (var guide in new[] { "POINT_OF_PRESENCE.md", "DEPLOYMENT.md" })
        {
            var text = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "docs", "guides", guide));
            text.Should().NotContain("cp .env.example .env.pop",
                $"{guide} would put every password of the installation on the point of presence");
        }
    }

    /// <remarks>
    /// A profile widens the default set, so the command names its services and the overlay clears their
    /// dependencies on the API. An API on the door would need the production secrets.
    /// </remarks>
    [Fact]
    public void RunNoApplicationOnTheSecondDoor()
    {
        var overlay = Read(PopCompose);
        overlay.Should().MatchRegex(@"depends_on: !(reset|override)",
            "compose starts the dependencies of a named service, and the edge declared one on the API");
        overlay.Should().Contain("pop.conf.template",
            "the door has its own edge configuration: its upstream is across the network, " +
            "and the shared one points at a container that does not run here");

        var documents = new[] { "POINT_OF_PRESENCE.md", "DEPLOYMENT.md" }
            .Select(name => File.ReadAllText(
                Path.Combine(RepositoryLayout.Root, "docs", "guides", name)));

        var commands = documents
            .SelectMany(document => document.Split('\n'))
            .Where(line => line.Contains("--env-file .env.pop", StringComparison.Ordinal))
            .ToList();

        commands.Should().NotBeEmpty("the guides still document how the door is started");
        foreach (var command in commands)
        {
            command.Should().Contain(PopCompose,
                "the base file alone starts every store the door has no business running");
            command.Should().Contain(PreviewCompose,
                "the edge and the frontend live in the overlay");

            var arguments = command[(command.IndexOf("up -d", StringComparison.Ordinal) + 5)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(argument => argument.Trim('`'))
                .ToList();

            arguments.Should().Contain("nginx",
                "the door serves from its own edge, and nothing else in the command starts one");
            arguments.Should().Contain("dmfront",
                "serving the frontend from the door is the reason it exists: markup, scripts and " +
                "styles stop crossing the network on every page");

            foreach (var elsewhere in new[] { "dmapi", "postgres", "storage", "seaweedfs", "migration" })
            {
                arguments.Should().NotContain(elsewhere,
                    $"{elsewhere} belongs to the main server, and a door that runs its own holds " +
                    "the secrets this deployment exists to keep away from it");
            }
        }
    }

    // On the door the compose service names resolve to nothing, so the upstream arrives as
    // configuration.
    [Fact]
    public void SendTheDoorsApiTrafficAcrossTheNetwork()
    {
        var door = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "docker", "nginx", "pop.conf.template"));

        door.Should().Contain("${POP_UPSTREAM}",
            "the address of the main server is deployment configuration");
        door.Should().Contain("proxy_cache",
            "caching media on the door is why it is a door and not a redirect");

        Regex.Matches(door, @"proxy_pass\s+http://dmapi")
            .Should().BeEmpty("the API does not run here");

        Read(PopCompose).Should().Contain("NGINX_ENVSUBST_FILTER",
            "without a filter the substitution eats $host and $scheme, which belong to nginx");
    }

    /// <summary>
    /// The door proxies the hub the API maps, and no path that was never there.
    /// </summary>
    [Fact]
    public void ProxyTheNotificationHubTheApiMaps()
    {
        var template = Read("nginx/pop.conf.template");

        template.Should().Contain("location /hubs/ {",
            "the API maps its SignalR hub at /hubs/notifications");
        template.Should().NotContain("/whatsup", "no such path exists in the API or the client");
    }
}
