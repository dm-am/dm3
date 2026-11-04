using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The two scripts that run the development stack have to mean the same thing.
/// </summary>
/// <remarks>The two scripts are the only way to run a local stack, so a defect in one is found on one platform.</remarks>
public class ManagementScriptsShould
{
    private const string PowerShellScript = "dm.ps1";
    private const string ShellScript = "dm.sh";

    /// <summary>
    /// The liveness route the container healthcheck asks for, read from the compose file.
    /// </summary>
    private static string LivenessRoute
    {
        get
        {
            var compose = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "docker", "docker-compose.yml"));
            var match = Regex.Match(compose, @"curl -f http://localhost:5000(/[A-Za-z0-9_/-]+)");
            match.Success.Should().BeTrue("the API healthcheck in docker-compose.yml names the liveness route");
            return match.Groups[1].Value;
        }
    }


    /// <summary>
    /// The line scripts/dm.ps1 loads one of its parts from scripts/dm with.
    /// </summary>
    private static readonly Regex PartLoad = new(
        @"^\. \(Join-Path \$PSScriptRoot ""dm\\(?<part>\w+\.ps1)""\)\r?$", RegexOptions.Multiline);

    /// <summary>
    /// A script under scripts/, with each part scripts/dm.ps1 dot-sources put
    /// in place of the line that loads it.
    /// </summary>
    /// <remarks>Read in the order PowerShell runs it; a load line naming a missing part fails the read.</remarks>
    internal static string Read(string script) =>
        PartLoad.Replace(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root, "scripts", script)),
            load => File.ReadAllText(Path.Combine(RepositoryLayout.Root, "scripts", "dm", load.Groups["part"].Value)));

    /// <summary>
    /// The addresses of the local services are written once, in scripts/dm/addresses.txt.
    /// </summary>
    /// <remarks>A third copy in either script is the drift this exists to close.</remarks>
    [Theory]
    [InlineData(PowerShellScript)]
    [InlineData(ShellScript)]
    public void ReadTheAddressesFromOneFile(string script)
    {
        var source = Read(script);

        source.Should().Contain("addresses.txt", $"scripts/{script} reads the shared list of addresses");
        Regex.IsMatch(source, @"https?://localhost:\d+").Should().BeFalse(
            $"scripts/{script} must not spell an address of its own beside the shared list");
    }

    /// <summary>
    /// A service deleted from the compose file leaves its container behind, and a plain "down" walks past it.
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
    /// Both scripts wait on the route the container healthcheck asks for.
    /// </summary>
    [Theory]
    [InlineData(PowerShellScript)]
    [InlineData(ShellScript)]
    public void AskTheHealthcheckRouteWhetherTheApiIsUp(string script)
    {
        // A curl invocation, not every mention of the address; the address itself is read
        // by both scripts from scripts/dm/addresses.txt into $apiUrl and $API_URL.
        var source = Read(script);
        var probes = Regex.Matches(source, @"curl[^\r\n]*?\$(?:apiUrl|API_URL)(?<route>/[A-Za-z0-9_/-]*)");

        probes.Should().NotBeEmpty($"scripts/{script} waits for the API");
        foreach (Match probe in probes)
        {
            probe.Groups["route"].Value.Should().Be(LivenessRoute,
                $"scripts/{script} must ask the route the healthcheck asks, not a product one");
        }
    }

    /// <summary>
    /// The API computes its periodic projections at start, so seeding restarts it.
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
    /// Every time the Windows script names itself for a person to run, it names
    /// itself in the form README gives, the one that starts from cmd as well as
    /// from PowerShell.
    /// </summary>
    /// <remarks>A bare .\scripts\dm.ps1 does not start from cmd.</remarks>
    [Fact]
    public void NameItselfTheWayEveryShellRunsIt()
    {
        var source = Read(PowerShellScript);
        var mentions = Regex.Matches(source, @"(?<before>[^\r\n]*?)\.\\scripts\\dm\.ps1");

        mentions.Should().NotBeEmpty("scripts/dm.ps1 tells the reader which of its commands to run next");

        var bare = mentions
            .Where(mention => !mention.Groups["before"].Value.EndsWith("powershell -File ", StringComparison.Ordinal))
            .Select(mention => mention.Value.Trim())
            .ToArray();

        // Joined, because an empty-collection failure prints the first offender
        // and the point is to name every line at once.
        string.Join("; ", bare).Should().BeEmpty(
            "every command scripts/dm.ps1 prints about itself is pasted into whatever shell is open, "
            + "and README's powershell -File form is the one that starts from cmd too");
    }

    /// <summary>
    /// No line the Windows script prints chains two commands with &amp;&amp;.
    /// </summary>
    /// <remarks>Windows PowerShell rejects &amp;&amp; as a statement separator.</remarks>
    [Fact]
    public void PrintNoCommandChainTheWindowsShellRejects()
    {
        var chained = Read(PowerShellScript)
            .Split('\n')
            .Where(line => line.Contains("Write-Host", StringComparison.Ordinal) && line.Contains("&&", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToArray();

        string.Join("; ", chained).Should().BeEmpty(
            "Windows PowerShell refuses && and a printed hint is run where it was printed");
    }

    /// <summary>
    /// The stand "e2e" prepares is switched the way CI switches its own, and the
    /// check the pre-push hook runs reads back that same switch.
    /// </summary>
    /// <remarks>Under the default limits the suite fails on 429s; the key is read out of the workflow.</remarks>
    [Fact]
    public void SwitchRateLimitingOffForTheSuiteTheWayContinuousIntegrationDoes()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml"));
        var overlay = Regex.Match(workflow, @"(?<key>DM_RateLimiting__\w+):\s*""false""");
        overlay.Success.Should().BeTrue("the e2e job of dotnet.yml turns rate limiting off with a compose overlay");
        var key = overlay.Groups["key"].Value;

        foreach (var script in new[] { PowerShellScript, ShellScript })
        {
            Regex.IsMatch(Read(script), Regex.Escape(key) + @":\s*`?""false`?""").Should().BeTrue(
                $"scripts/{script} e2e has to give the API the switch the e2e job gives it");
        }

        Read("check-e2e-stand.sh").Should().Contain($"{key}=false",
            "the hook's check has to look for the switch the e2e command sets");
    }

    /// <summary>
    /// The stand "e2e" prepares names the commit its API was built on, and the
    /// check the pre-push hook runs refuses an API built before the server
    /// changes being pushed.
    /// </summary>
    /// <remarks>The image is built once by "e2e", so without the record the suite measures an older server.</remarks>
    [Fact]
    public void RefuseASuiteAgainstAnApiBuiltBeforeTheServerBeingPushed()
    {
        const string key = "E2E_SOURCE_COMMIT";

        foreach (var script in new[] { PowerShellScript, ShellScript })
        {
            var source = Read(script);
            source.Should().Contain($"{key}:",
                $"scripts/{script} e2e has to write the commit it builds on into the container");
            source.Should().Contain("rev-parse HEAD",
                $"scripts/{script} e2e names the commit the tree is at when the image is built");
        }

        var check = Read("check-e2e-stand.sh");
        check.Should().Contain(key, "the check reads back the commit the e2e command wrote");
        Regex.IsMatch(check, @"git\b[^\n]*\bdiff --quiet ""\$\w+"" HEAD").Should().BeTrue(
            "the check compares the server between the commit the API was built on and HEAD");
        check.Should().Contain(":(exclude)src/DM.Web.Client",
            "the client is built by the suite itself from the tree, so only the server can be stale");
    }

    /// <summary>
    /// The environment is prepared and then checked, in that order.
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

    /// <summary>
    /// The status screen tells a client test run and a client build from a dev
    /// server.
    /// </summary>
    /// <remarks>Every vitest worker says "vite" too; the rules are applied as PowerShell applies them, first match, case ignored.</remarks>
    [Theory]
    [InlineData(@"""node""   ""D:\Projects\Web\dm3\src\DM.Web.Client\node_modules\.bin\\..\vite\bin\vite.js"" ", "vite dev server")]
    [InlineData(@"""node""   ""D:\Projects\Web\dm3\src\DM.Web.Client\node_modules\.bin\\..\vite\bin\vite.js"" preview --config vite.preview.config.ts --port 5174", "vite dev server")]
    [InlineData(@"""node""   ""D:\Projects\Web\dm3\src\DM.Web.Client\node_modules\.bin\\..\vite\bin\vite.js"" build", "vite build")]
    [InlineData(@"""node""   ""D:\Projects\Web\dm3\src\DM.Web.Client\node_modules\.bin\\..\vitest\vitest.mjs"" run", "vitest")]
    [InlineData(@"""C:\Program Files\nodejs\node.exe"" --require D:/Projects/Web/dm3/src/DM.Web.Client/node_modules/vitest/suppress-warnings.cjs --conditions node D:\Projects\Web\dm3\src\DM.Web.Client\node_modules\vitest\dist\workers\forks.js", "vitest")]
    public void TellATestRunFromADevServer(string commandLine, string kind)
    {
        var rules = Regex.Matches(Read(PowerShellScript),
                @"\$command -match '(?<pattern>[^']+)'\) \{ \$kind = '(?<kind>[^']+)' \}")
            .Select(rule => (Pattern: rule.Groups["pattern"].Value, Kind: rule.Groups["kind"].Value))
            .ToArray();
        rules.Should().NotBeEmpty("Get-HostProcesses names each process it lists by its command line");

        var named = rules.FirstOrDefault(rule => Regex.IsMatch(commandLine, rule.Pattern, RegexOptions.IgnoreCase)).Kind;

        named.Should().Be(kind);
    }
}
