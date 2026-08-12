using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The two scripts that run the development stack have to mean the same thing.
/// </summary>
/// <remarks>
/// scripts/dm.ps1 and scripts/dm.sh document one set of commands and are the
/// only way anybody starts, resets or seeds a local stack, so a defect in one of
/// them is found by the developer on that platform and by nobody else. They
/// drifted exactly that way: seeding restarted the API on Windows and did not on
/// Linux, and the two asked different routes to decide the API was up.
///
/// Nothing here duplicates a value the scripts own. Each assertion names a
/// behaviour that had already been lost once and would be lost again silently.
/// </remarks>
public class ManagementScriptsShould
{
    private const string PowerShellScript = "dm.ps1";
    private const string ShellScript = "dm.sh";

    /// <summary>
    /// The liveness route the container healthcheck asks for. Read from the
    /// compose file rather than repeated here: the point of the assertion is
    /// that the scripts use the same route as the stack, so the stack has to be
    /// the one that says what it is.
    /// </summary>
    private static string LivenessRoute
    {
        get
        {
            var compose = File.ReadAllText(Path.Combine(RepositoryRoot, "docker", "docker-compose.yml"));
            var match = Regex.Match(compose, @"curl -f http://localhost:5000(/[A-Za-z0-9_/-]+)");
            match.Success.Should().BeTrue("the API healthcheck in docker-compose.yml names the liveness route");
            return match.Groups[1].Value;
        }
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string Read(string script) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", script));

    /// <summary>
    /// A service deleted from the compose file leaves its container behind, and
    /// a plain "down" walks past it. The search worker and its Elasticsearch
    /// were removed from the stack and survived every reset for months after,
    /// holding their names and their place on the network, while the command
    /// whose whole job is to leave nothing behind reported success.
    /// </summary>
    [Theory]
    [InlineData(PowerShellScript)]
    [InlineData(ShellScript)]
    public void RemoveOrphansOnEveryComposeDown(string script)
    {
        var source = Read(script);
        var downs = Regex.Matches(source, @"compose down(?<flags>[^\r\n""]*)");

        downs.Should().NotBeEmpty($"scripts/{script} stops the stack");
        foreach (Match down in downs)
        {
            down.Groups["flags"].Value.Should().Contain("--remove-orphans",
                $"a container of a service the project no longer declares outlives every "
                + $"reset otherwise, and scripts/{script} reports that as success");
        }
    }

    /// <summary>
    /// Both scripts wait on the same route, and it is the one the container
    /// healthcheck already asks for. Pinned to a product route instead, the
    /// seed command depended on one controller keeping its path and staying
    /// anonymous - and the two scripts named two different controllers.
    /// </summary>
    [Theory]
    [InlineData(PowerShellScript)]
    [InlineData(ShellScript)]
    public void AskTheHealthcheckRouteWhetherTheApiIsUp(string script)
    {
        // A curl invocation, not every mention of the address: both scripts
        // print the API address in their help and in their messages, and those
        // are text rather than a probe.
        var source = Read(script);
        var probes = Regex.Matches(source, @"curl[^\r\n]*?http://localhost:5000(?<route>/[A-Za-z0-9_/-]*)");

        probes.Should().NotBeEmpty($"scripts/{script} waits for the API");
        foreach (Match probe in probes)
        {
            probe.Groups["route"].Value.Should().Be(LivenessRoute,
                $"scripts/{script} must ask the route the healthcheck asks, not a product one");
        }
    }

    /// <summary>
    /// The API computes its periodic projections at start - popularity, the best
    /// post of the week - so a stack seeded without a restart keeps showing what
    /// it worked out over the empty database, with nothing on screen to say so.
    /// dm.ps1 restarted it and dm.sh did not, which is two outcomes from one
    /// documented command.
    /// </summary>
    [Theory]
    [InlineData(PowerShellScript)]
    [InlineData(ShellScript)]
    public void RestartTheApiAfterSeeding(string script)
    {
        var source = Read(script);

        source.Should().Contain("restart dm-api",
            $"scripts/{script} seeds the database behind the running API, and the "
            + "projections it computed at start are stale from that moment on");
    }

    /// <summary>
    /// The environment is prepared and then checked, in that order. The check
    /// used to run first and on its own, so a fresh clone was told to copy the
    /// template by hand while the script that owns that file sat unused - the
    /// documented first run, dead on Windows and working everywhere else.
    /// </summary>
    [Fact]
    public void PrepareTheEnvironmentBeforeAssertingIt()
    {
        var source = Read(PowerShellScript);

        var initialise = source.IndexOf("function Initialize-Environment", StringComparison.Ordinal);
        initialise.Should().BeGreaterThan(-1, "the two steps are ordered in one place");

        var body = source[initialise..];
        var generate = body.IndexOf("Invoke-EnvironmentInit", StringComparison.Ordinal);
        var assert = body.IndexOf("Assert-Environment", StringComparison.Ordinal);

        generate.Should().BeGreaterThan(-1, "Initialize-Environment runs the generator that owns docker/.env");
        assert.Should().BeGreaterThan(generate, "the file is created before it is checked for completeness");

        var dispatcher = source[source.LastIndexOf("# Main", StringComparison.Ordinal)..];
        dispatcher.Should().Contain("Initialize-Environment",
            "every command that reaches compose goes through the same preparation");
        dispatcher.Should().NotContain("Assert-Environment",
            "asserting without preparing first is what left a clone unable to start");
    }
}
