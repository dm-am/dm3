using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing an agent is handed starts a server on the port the owner has open.
/// </summary>
/// <remarks>
/// The owner's dev server is pinned with strictPort, so there are two outcomes.
/// Either the agent's server refuses to start on a busy port, or it takes the
/// port while the owner's is down and the tab he has open is served by a
/// different build without saying so. The second one is the expensive one:
/// whatever is measured there gets reported as a fact about his stand.
///
/// The trap has been closed twice already and came back both times, once as a
/// second launch configuration and once as a default base URL for the e2e runner.
/// Both were found by reading, which is why no port is written here. It is read
/// out of the config that pins it, and the npm scripts that start that server are
/// read out of package.json, so a renamed script or a moved port keeps the rule
/// pointed at the right thing.
///
/// Only fenced blocks are searched. What gets pasted into a shell is what sits in
/// a fence, and a brief has to stay able to name in prose the command it forbids.
/// </remarks>
public class AgentToolingShould
{
    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string ClientPath(string file) =>
        Path.Combine(RepositoryRoot, "src", "DM.Web.Client", file);

    /// <summary>The port the owner's server pins, read out of the config that pins it.</summary>
    private static string OwnerPort()
    {
        var declaration = Regex.Match(
            File.ReadAllText(ClientPath("vite.config.ts")),
            @"server:\s*\{\s*port:\s*(\d+)");

        declaration.Success.Should().BeTrue("the dev server declares its port in vite.config.ts");
        return declaration.Groups[1].Value;
    }

    /// <summary>
    /// The npm scripts that start that server. "vite build" and "vite preview" are
    /// other programs sharing the executable, and a script pointed at another
    /// config is pointed at another port.
    /// </summary>
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
        Directory.GetFiles(Path.Combine(RepositoryRoot, ".claude", "agents"), "*.md");

    /// <summary>The lines a brief hands over to be run, as opposed to the prose about them.</summary>
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

    /// <summary>
    /// Commands that destroy the owner's data, spelled however the project spells
    /// them.
    /// </summary>
    /// <remarks>
    /// The port rule above and this one come from the same fact — a subagent runs
    /// what its brief hands it — and only one of them was written down. The
    /// debugger's brief listed `dm.ps1 reset` under the heading "Restart
    /// services", and that command is `compose down -v`: every account, game and
    /// post on the stand, gone, because a diagnosis wanted a fresh log.
    ///
    /// Matched on the wrapper as well as on the underlying compose flag. Checking
    /// only for `-v` would pass the wrapper, which is the spelling a brief
    /// actually uses, and checking only for the wrapper would pass the raw
    /// command a brief might grow later.
    /// </remarks>
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
            File.ReadAllText(Path.Combine(RepositoryRoot, ".claude", "launch.json")));

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
}
