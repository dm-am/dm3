using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// Why it is recreated is in DEPLOYMENT.md, section "Край держит адреса с момента старта"; the
/// headers are EdgeSecurityHeadersShould's.
/// </remarks>
public class EdgeConfigurationShould
{
    /// <remarks>
    /// A thumbnail lies beside its original in the store and shares its address, so the route of the
    /// store is the only one a picture needs. Every gate serves the SPA without nginx, so the route is
    /// asserted in the configuration.
    /// </remarks>
    [Fact]
    public void ServeThumbnailsByTheRouteOfTheirOriginals()
    {
        var bucket = Regex.Match(
            Read(BaseCompose), @"DM_CdnConfiguration__BucketName: \$\{STORAGE_BUCKET:-([a-z0-9.-]+)\}");
        bucket.Success.Should().BeTrue("the base stack names the bucket every address is built under");

        var running = ActiveDirectives(EdgeAsAssembled());
        var route = Between(running, $"location /{bucket.Groups[1].Value}/ {{", "}");

        route.Should().NotBeNull(
            "the API hands out an address under the bucket for every picture, and nginx routes by prefix");
        route.Should().Contain($"proxy_pass http://storage:9000/{bucket.Groups[1].Value}/;",
            "the store publishes on loopback, so its proxy on the application network is the only way in");
        running.Should().NotContain("location /img/",
            "a thumbnail is cut at upload and has no route of its own, and a route to nothing is a door " +
            "that answers 502");
    }

    /// <remarks>
    /// The pipeline and the operator check the same list. The readiness probe is named: liveness
    /// answers green from a process that reaches no database.
    /// </remarks>
    [Fact]
    public void CheckTheEdgeThroughOneListOfAssertions()
    {
        var script = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "smoke-edge.sh"));
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml"));

        var smoke = Between(workflow,
            "- name: The edge answers where the deployment needs it to", "- name: Logs on failure");

        smoke.Should().Contain("smoke-edge.sh",
            "the pipeline and the operator have to be checking the same things, and the only " +
            "way to keep two lists equal is to have one");
        smoke.Should().NotContain("curl",
            "a second copy of the assertions beside the call to the script is the drift this " +
            "exists to close");

        script.Should().Contain("/_ready",
            "this is the probe an external watcher polls: liveness answers green from a " +
            "process that reaches neither database");
        script.Should().Contain("401",
            "the stand is closed at the edge, and a smoke test that never sees a refusal " +
            "would pass just as well against an open one");
    }

    /// <remarks>
    /// The challenge path is answered over plain http before the basic-auth door by both edges;
    /// `certbot --nginx` cannot edit a read-only mounted configuration.
    /// </remarks>
    [Fact]
    public void LeaveARoadToACertificateAtBothEdges()
    {
        var edge = Read(NginxConfiguration);
        var pointOfPresence = Read("nginx/pop.conf.template");

        foreach (var (name, configuration) in new[]
                 {
                     (NginxConfiguration, edge),
                     ("pop.conf.template", pointOfPresence),
                 })
        {
            ActiveDirectives(configuration).Should().Contain("location ^~ /.well-known/acme-challenge/",
                $"{name} has to answer the challenge over plain http, and ^~ so it wins over " +
                "the prefix that proxies the SPA");
        }

        ActiveDirectives(edge).Should().Contain("auth_basic off",
            "the authority fetches the challenge anonymously, so the door cannot stand in front of it");

        // The copy is gone, and so is the shape it was written in.
        edge.Should().NotContain("listen 443 ssl http2",
            "nginx has warned about that form since 1.25 and the image is newer than that");
        edge.Should().Contain("include /etc/nginx/edge-locations.conf;",
            "one set of locations for both servers, because the copy is what drifted");
        pointOfPresence.Should().NotContain("listen 443 ssl http2",
            "the same form, and the point of presence is the edge that already serves TLS");

        var script = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "init-ssl.sh"));
        script.Should().Contain("certonly --webroot",
            "--nginx has nothing to edit: the configuration is mounted read-only into a container");
        script.Should().Contain("--deploy-hook",
            "nginx reads the certificate at start-up, so a renewal nobody reloads for is a " +
            "certificate that expires while a fresh one sits on disk");

        File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh")).Should().Contain("certbot",
            "the tool the documented procedure runs has to be installed by the installer");
    }

    /// <summary>
    /// The edge is recreated whenever a container it resolves by name is replaced.
    /// </summary>
    /// <remarks>
    /// nginx resolves every upstream once, and a variable in proxy_pass drops the URI two routes
    /// need. Compose never restarts an unhealthy container, so the updater's labels are the
    /// mechanism, asserted both ways.
    /// </remarks>
    [Fact]
    public void RecreateTheEdgeWhenAContainerItResolvesIsReplaced()
    {
        var stack = Read(BaseCompose) + "\n" + Read(PreviewCompose);
        var edge = ServiceBlock(Read(PreviewCompose), "nginx");

        edge.Should().Contain("watchtower.enable=true",
            "with WATCHTOWER_LABEL_ENABLE a container without the label does not exist as " +
            "far as the updater is concerned, so no linked restart can apply to it");

        var declared = EdgeDependencies(edge);
        declared.Should().NotBeEmpty(
            "the edge holds the addresses of the containers behind it, so something has to " +
            "put it back on its feet after one of them is replaced");

        // Forwards: everything the edge resolves by name and the updater replaces.
        foreach (var host in ResolvedHosts(File.ReadAllText(
            Path.Combine(DockerDirectory, "nginx", "nginx.conf"))))
        {
            if (!Regex.IsMatch(stack, $@"^  {Regex.Escape(host)}:", RegexOptions.Multiline)) continue;
            if (!ServiceBlock(stack, host).Contains("watchtower.enable=true", StringComparison.Ordinal)) continue;

            var containerName = Regex.Match(ServiceBlock(stack, host), @"container_name:\s*'([\w-]+)'");
            containerName.Success.Should().BeTrue($"{host} is a service of the stack and has a container name");
            declared.Should().Contain(containerName.Groups[1].Value,
                $"the updater replaces {host} on its own schedule, and the edge resolved it once");
        }

        // Backwards: a name in the list that the updater never touches.
        foreach (var dependency in declared)
        {
            ServiceWithContainerName(stack, dependency).Should().Contain("watchtower.enable=true",
                $"{dependency} is named as a reason to recreate the edge, and the updater " +
                "does not watch it, so it never is one");
        }

        // The point of presence carries no application, and a list inherited from the
        // main stand would name a container that does not exist there.
        var pointOfPresence = ServiceBlock(Read(PopCompose), "nginx");
        var api = Regex.Match(Read(BaseCompose), @"^  dmapi:\n(?:.*\n)*?\s*container_name:\s*'([\w-]+)'",
            RegexOptions.Multiline);

        api.Success.Should().BeTrue("the base stack names the API container");

        // Stated as a replacement: the inherited list names the API.
        pointOfPresence.Should().Contain("labels: !override",
            "silence here is not an empty list, it is the list of the main stand");
        EdgeDependencies(pointOfPresence).Should().NotBeEmpty(
            "the edge of the point of presence holds addresses the same way the main one does")
            .And.NotContain(api.Groups[1].Value,
            "the API does not run at the point of presence, and a dependency on a container " +
            "that is never there is a line that reads like ordering and is not");
    }

    /// <summary>
    /// The edge accepts a body as large as the endpoint behind it does.
    /// </summary>
    /// <remarks>nginx defaults to one megabyte, and its HTML 413 carries no CORS header the form could read.</remarks>
    [Fact]
    public void AcceptAtTheEdgeEverythingTheUploadEndpointAccepts()
    {
        var controller = File.ReadAllText(Path.Combine(RepositoryLayout.Root,
            "src", "DM.Web.API", "Features", "General", "Upload", "UploadController.cs"));

        var endpoint = Regex.Match(controller, @"RequestSizeLimit\((\d+) \* 1024 \* 1024\)");
        endpoint.Success.Should().BeTrue("the upload endpoint declares its own ceiling");

        // Both public edges: the point of presence proxies the upload path upstream.
        foreach (var name in new[] { NginxConfiguration, "nginx/pop.conf.template" })
        {
            var edge = Regex.Match(
                ActiveDirectives(Read(name)), @"client_max_body_size (\d+)m;");
            edge.Success.Should().BeTrue(
                $"without the directive {name} cuts every body at its own default of 1 MB");

            edge.Groups[1].Value.Should().Be(endpoint.Groups[1].Value,
                $"a lower limit in {name} refuses what the endpoint accepts, and a higher one " +
                "carries the whole body across the network to be refused at the end of it");
        }
    }

    /// <remarks>
    /// Without its own location /_health falls into the SPA fallback, which answers 200. Exact matches:
    /// /_health/detail and /metrics stay inside.
    /// </remarks>
    [Fact]
    public void AnswerTheExternalHeartbeatAtTheEdge()
    {
        var nginx = Read(NginxConfiguration);

        // Both public edges: without their own locations these paths fall into the SPA prefix.
        foreach (var server in new[]
                 {
                     ActiveDirectives(EdgeAsAssembled()),
                     ActiveDirectives(Read("nginx/pop.conf.template")),
                 })
        {
            server.Should().Contain("location = /_health {",
                "the heartbeat the guide recommends has to reach the API, not the SPA fallback");
            server.Should().Contain("location = /_ready {",
                "readiness answers for the stores, and a stand that cannot reach Postgres is down");
            server.Should().NotContain("location /_health",
                "a prefix location publishes /_health/detail, which names every dependency");
            server.Should().NotContain("location /metrics",
                "the instrumentation of the process stays on loopback");
        }
    }

    /// <summary>Container names the edge is told to wait for, as the label lists them.</summary>
    private static string[] EdgeDependencies(string serviceBlock) => Regex
        .Match(serviceBlock, @"watchtower\.depends-on=([^""\s]+)")
        .Groups[1].Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Hosts an nginx configuration resolves by name and then holds; names with a variable re-resolve.
    /// </summary>
    private static IEnumerable<string> ResolvedHosts(string configuration) => configuration
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'))
        .SelectMany(line => new[]
        {
            Regex.Match(line, @"^server\s+([a-z0-9][a-z0-9._-]*):\d+;"),
            Regex.Match(line, @"^proxy_pass\s+https?://([a-z0-9][a-z0-9._-]*)"),
        })
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .Where(host => !host.Contains('$') && !host.Contains('{'))
        .Distinct();
}
