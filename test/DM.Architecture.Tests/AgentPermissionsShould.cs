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
/// The list that decides what runs without asking stays narrow where it matters.
/// </summary>
/// <remarks>
/// The hook in .claude/hooks refuses the commands that lose uncommitted work, and
/// it is the only thing that does. A wildcard entry here is the other side of the
/// same door: it pre-approves every form of a verb, including the ones nobody
/// thought about when the entry was written, and the whole list had one for git,
/// one for rm and one for the shell interpreter at the same time.
///
/// Read and build commands stay wide on purpose. They do not write outside the
/// checkout and do not reach the network, so approving them one by one buys
/// nothing but interruptions.
/// </remarks>
public class AgentPermissionsShould
{
    /// <summary>
    /// A verb that either loses work, reaches the network, or runs whatever it is
    /// handed. None of them may appear with a bare wildcard.
    /// </summary>
    private static readonly string[] Dangerous =
    [
        "git", "rm", "del", "curl", "wget", "ping", "kill", "pkill", "taskkill",
        "bash", "sh", "powershell", "powershell.exe", "pwsh", "cmd", "cmd.exe",
        "winget", "npm publish", "docker system prune",
    ];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IReadOnlyList<string> Entries(string settingsFile, string section)
    {
        var path = Path.Combine(RepositoryRoot, ".claude", settingsFile);
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("permissions", out var permissions) ||
            !permissions.TryGetProperty(section, out var entries))
        {
            return [];
        }

        return entries.EnumerateArray().Select(entry => entry.GetString() ?? string.Empty).ToList();
    }

    /// <summary>
    /// Matches "Bash(verb:*)" and "Bash(verb *)": the two spellings of "anything
    /// this verb can be handed".
    /// </summary>
    private static string? BareWildcardVerb(string entry)
    {
        var match = Regex.Match(entry, @"^Bash\(([^:()\s]+(?:\.exe)?)\s*(?::\*|\*)\)$");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Fact]
    public void PreApproveNoDangerousVerbWithAWildcard()
    {
        var offenders = new List<string>();

        foreach (var file in new[] { "settings.json", "settings.local.json" })
        {
            offenders.AddRange(Entries(file, "allow")
                .Where(entry =>
                {
                    var verb = BareWildcardVerb(entry);
                    return verb != null && Dangerous.Contains(verb, StringComparer.OrdinalIgnoreCase);
                })
                .Select(entry => $"{file}: {entry}"));
        }

        offenders.Should().BeEmpty(
            "a wildcard on one of these verbs approves every form of it in advance, " +
            "including the forms the hook exists to refuse; the narrow spelling " +
            "(\"Bash(git log:*)\", \"Bash(curl http://localhost:*)\") approves the use " +
            "that is actually needed");
    }

    /// <summary>
    /// The refusals are written down rather than left implicit in the absence of an
    /// approval, because the next reader of this file learns the rule from what it
    /// says, not from what it omits.
    /// </summary>
    [Fact]
    public void NameTheRefusalsTheHookEnforces()
    {
        var deny = Entries("settings.local.json", "deny")
            .Concat(Entries("settings.json", "deny"))
            .ToList();

        foreach (var forbidden in new[] { "git checkout", "git reset --hard", "git clean", "git stash drop" })
        {
            deny.Should().Contain(entry => entry.Contains(forbidden, StringComparison.Ordinal),
                $"CLAUDE.md forbids {forbidden} and the hook blocks it, so the settings " +
                "must not read as though it were merely unapproved");
        }
    }
}
