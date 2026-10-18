using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
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
    /// Every time the Windows script names itself for a person to run, it names
    /// itself in the form README gives, the one that starts from cmd as well as
    /// from PowerShell.
    /// </summary>
    /// <remarks>
    /// A bare .\scripts\dm.ps1 is a PowerShell-only spelling: pasted into cmd it
    /// does not start the script at all. The script printed that form after every
    /// start, in its help, and in each of its "run this first" refusals, so the
    /// next command a reader typed came from a line that did not run where he
    /// typed it.
    /// </remarks>
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
    /// <remarks>
    /// The script runs under Windows PowerShell, and that shell rejects &amp;&amp;
    /// as a statement separator: the hint for the frontend, printed at the end of
    /// every start, failed to parse in the window it was printed in.
    /// </remarks>
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
    /// <remarks>
    /// Under the default limits one page load spends a dozen and more requests
    /// of a hundred a minute, so the suite fails on 429s that look exactly like
    /// stale selectors. The key is read out of the workflow rather than written
    /// here: the point is that three places agree with the one CI runs under,
    /// and a renamed key there has to take the scripts with it.
    /// </remarks>
    [Fact]
    public void SwitchRateLimitingOffForTheSuiteTheWayContinuousIntegrationDoes()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "dotnet.yml"));
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
    /// <remarks>
    /// The image is built once, by "e2e", and nothing rebuilds it after that.
    /// Without the record the check found the API up and unlimited and let the
    /// suite measure whichever server had been built last, so a server change
    /// committed after it went out with a verdict about the code before it,
    /// wrong in either direction.
    /// </remarks>
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

    /// <summary>
    /// The status screen tells a client test run and a client build from a dev
    /// server.
    /// </summary>
    /// <remarks>
    /// A dev server was any node whose command line said "vite", and every
    /// vitest worker says it too: one test run listed a dozen dev servers and
    /// warned that two were up while the one the tab shows stood alone. The
    /// rules are read out of the script and applied the way PowerShell applies
    /// them, the first match winning and case ignored, to the command lines the
    /// two really run with.
    /// </remarks>
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
        rules.Should().NotBeEmpty("Show-HostProcesses names each process it lists by its command line");

        var named = rules.FirstOrDefault(rule => Regex.IsMatch(commandLine, rule.Pattern, RegexOptions.IgnoreCase)).Kind;

        named.Should().Be(kind);
    }
}
