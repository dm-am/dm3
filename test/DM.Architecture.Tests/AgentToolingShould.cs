using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing an agent is handed starts a server on the port the owner has open.
/// </summary>
/// <remarks>
/// The port and the npm scripts are read out of their configs, so a move keeps the rule pointed right.
/// Only fenced blocks are searched: a brief may name in prose the command it forbids.
/// </remarks>
public partial class AgentToolingShould
{

    private static string ClientPath(string file) =>
        Path.Combine(RepositoryLayout.Root, "src", "DM.Web.Client", file);

    private static string OwnerPort()
    {
        var declaration = Regex.Match(
            File.ReadAllText(ClientPath("vite.config.ts")),
            @"server:\s*\{\s*port:\s*(\d+)");

        declaration.Success.Should().BeTrue("the dev server declares its port in vite.config.ts");
        return declaration.Groups[1].Value;
    }

    // "vite build" and "vite preview" are other programs sharing the executable, and a script
    // pointed at another config is pointed at another port.
    private static bool StartsTheDevServer(string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0
               && words[0] == "vite"
               && (words.Length == 1 || words[1].StartsWith('-'))
               && !command.Contains("--config", StringComparison.Ordinal);
    }

    private static string[] DevServerScripts()
    {
        using var package = JsonDocument.Parse(File.ReadAllText(ClientPath("package.json")));

        return package.RootElement.GetProperty("scripts").EnumerateObject()
            .Where(script => StartsTheDevServer(script.Value.GetString() ?? string.Empty))
            .Select(script => script.Name)
            .ToArray();
    }

    private static string[] Briefs() =>
        Directory.GetFiles(Path.Combine(RepositoryLayout.Root, ".claude", "agents"), "*.md");

    private static IEnumerable<(string Brief, string Line)> FencedLines(string path)
    {
        var brief = new FileInfo(path).Name;
        var inside = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
            }
            else if (inside)
            {
                yield return (brief, line);
            }
        }
    }

    [Fact]
    public void HandOutNoCommandThatBindsTheOwnersPort()
    {
        var port = OwnerPort();
        var scripts = DevServerScripts();
        var briefs = Briefs();

        scripts.Should().NotBeEmpty("the rule below is written in terms of those scripts");
        briefs.Should().NotBeEmpty("the briefs are what this reads");

        var offenders = briefs
            .SelectMany(FencedLines)
            .Where(command =>
                command.Line.Contains(port, StringComparison.Ordinal) ||
                scripts.Any(script =>
                    command.Line.Contains($"npm run {script}", StringComparison.Ordinal)))
            .Select(command => $"{command.Brief}: {command.Line.Trim()}")
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a subagent runs what its brief hands it, and this binds the port the " +
            "owner's browser is pointed at, see the class remarks");
    }

    // Matched on the wrapper and on the compose flag alike: a brief may use either.
    private static readonly string[] DestroysTheStand =
    {
        "dm.ps1 reset",
        "down -v",
        "down --volumes",
        "volume rm",
        "volume prune",
        "system prune",
    };

    [Fact]
    public void HandOutNoCommandThatWipesTheOwnersData()
    {
        var briefs = Briefs();
        briefs.Should().NotBeEmpty("the briefs are what this reads");

        var offenders = briefs
            .SelectMany(FencedLines)
            .Where(command => DestroysTheStand.Any(destructive =>
                command.Line.Contains(destructive, StringComparison.OrdinalIgnoreCase)))
            .Select(command => $"{command.Brief}: {command.Line.Trim()}")
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a subagent runs what its brief hands it, and these empty the databases " +
            "the owner's stand is running on");
    }

    [Fact]
    public void RecogniseADestructiveCommandWhenItSeesOne()
    {
        // The rule is a substring scan, and a scan that stops matching passes in
        // silence.
        DestroysTheStand.Should().Contain(destructive =>
            ".\\scripts\\dm.ps1 reset".Contains(destructive, StringComparison.OrdinalIgnoreCase),
            "this is the line the rule exists for");
        DestroysTheStand.Should().Contain(destructive =>
            "docker compose down -v --remove-orphans".Contains(destructive, StringComparison.OrdinalIgnoreCase));
        DestroysTheStand.Should().NotContain(destructive =>
            ".\\scripts\\dm.ps1 status".Contains(destructive, StringComparison.OrdinalIgnoreCase),
            "reading the state of the stand is what these briefs are for");
    }

    [Fact]
    public void PreviewOnAPortOfItsOwn()
    {
        using var launch = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".claude", "launch.json")));

        var declared = launch.RootElement.GetProperty("configurations").EnumerateArray()
            .Where(configuration => configuration.TryGetProperty("port", out _))
            .Select(configuration => configuration.GetProperty("port").GetInt32())
            .ToArray();

        var port = OwnerPort();

        declared.Should().NotBeEmpty("a preview configuration names the port it binds");
        declared.Should().NotContain(preview => $"{preview}" == port,
            "a preview started from here would serve the owner's open tab out of the " +
            "assistant's build, and the browser shows no difference");
    }

    /// <summary>
    /// The end-to-end suite runs only after its stand has been checked, and its
    /// report never holds the run open.
    /// </summary>
    /// <remarks>
    /// A missing stand is refused in a second rather than twenty minutes in, and the HTML report
    /// would hold scripts/gates.sh open waiting on Ctrl+C.
    /// </remarks>
    [Theory]
    [InlineData("hooks/pre-push")]
    [InlineData("gates.sh")]
    public void CheckTheStandBeforeTheEndToEndSuite(string script)
    {
        var code = ShellLines(Path.Combine(RepositoryLayout.Root, "scripts", script)).ToList();

        var suite = code.FindIndex(line => line.Contains("npm run test:e2e:fast", StringComparison.Ordinal));
        var check = code.FindIndex(line => line.Contains("check-e2e-stand.sh", StringComparison.Ordinal));
        var firstCostlyGate = code.FindIndex(line => line.Contains("npm ci --dry-run", StringComparison.Ordinal));

        suite.Should().BeGreaterThan(-1, $"scripts/{script} runs the end-to-end suite");
        firstCostlyGate.Should().BeGreaterThan(-1, $"scripts/{script} opens the gates of the branch with the lock file");
        check.Should().BeGreaterThan(-1,
            $"scripts/{script} runs a suite that needs a stand, and has to say so before it starts");
        check.Should().BeLessThan(firstCostlyGate,
            "a missing stand is learnt before the gates that take minutes, not after them");
        code[suite].Should().Contain("PLAYWRIGHT_HTML_OPEN=never",
            "a failed run that opens its report waits on Ctrl+C instead of ending");
    }

    // The hook names the command to fix the stand; running it would restart the stack under an open
    // tab.
    [Fact]
    public void LeaveTheStandAsTheCheckFoundIt()
    {
        var code = ShellLines(Path.Combine(RepositoryLayout.Root, "scripts", "check-e2e-stand.sh")).ToArray();

        var acting = code
            .Where(line =>
                Regex.IsMatch(line, @"\bdocker\s+(?:compose|start|restart|run|rm|stop|kill|exec|container|network|volume)\b") ||
                (Regex.IsMatch(line, @"\bdm\.(?:ps1|sh)\b") && !line.Contains("echo", StringComparison.Ordinal)))
            .Select(line => line.Trim());

        Named(acting).Should().BeEmpty(
            "scripts/check-e2e-stand.sh reads the stand and names the command that prepares it; " +
            "running that command is the developer's call");
    }

    // The offenders as one line, because an empty-collection failure prints the first item and
    // these lists exist to name every place at once.
    private static string Named(IEnumerable<string> offenders) =>
        string.Join("; ", offenders.OrderBy(offender => offender, StringComparer.Ordinal));
}
