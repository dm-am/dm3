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
    /// <summary>
    /// Walks up from the test binary to the repository root. Neither the briefs
    /// nor the client configs are copied to the output directory, and copying them
    /// would let this assert against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, ".claude")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "src"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

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
