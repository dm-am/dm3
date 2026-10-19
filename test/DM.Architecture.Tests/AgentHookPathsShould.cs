using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The hooks of the agent settings are found from every directory an agent works in.
/// </summary>
/// <remarks>Apart from <see cref="AgentPermissionsShould"/>: the two fail for unrelated reasons.</remarks>
public class AgentHookPathsShould
{

    /// <summary>The variable the agent sets to the root of the checkout before it runs a hook.</summary>
    private const string ProjectDirectory = "$CLAUDE_PROJECT_DIR/";

    /// <summary>
    /// Every hook runs from wherever the agent stands.
    /// </summary>
    /// <remarks>A hook runs in the agent's current directory, so a path relative to the root is not found below it.</remarks>
    [Fact]
    public void ReachEveryHookFromWhereverTheAgentStands()
    {
        var settings = File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".claude", "settings.json"));

        HookCommands(settings).Should().NotBeEmpty(
            "the settings run the hooks this class reads, and a walk that finds no command checks nothing");
        UnreachableHooks(settings).Should().HaveCount(0,
            "a hook addressed from the current directory fails for every agent standing anywhere " +
            "but the root, and the refusal it holds silently stops applying there");
    }

    /// <summary>
    /// The rule refuses both ways a hook stops being found: a script named from
    /// the current directory, and one named from the root that is not there.
    /// </summary>
    [Theory]
    [InlineData("node .claude/hooks/block-dangerous-git.js")]
    [InlineData("node \\\"$CLAUDE_PROJECT_DIR/.claude/hooks/no-such-hook.js\\\"")]
    public void RefuseAHookTheAgentCannotReachFromEverywhere(string command)
    {
        var settings = $$"""
            { "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [
                { "type": "command", "command": "{{command}}" } ] } ] } }
            """;

        UnreachableHooks(settings).Should().ContainSingle();
    }

    /// <summary>Every command a hook of the settings runs.</summary>
    private static IReadOnlyList<string> HookCommands(string settings)
    {
        using var document = JsonDocument.Parse(settings);
        if (!document.RootElement.TryGetProperty("hooks", out var hooks))
        {
            return [];
        }

        var commands = new List<string>();
        Collect(hooks, commands);
        return commands;

        static void Collect(JsonElement element, List<string> commands)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name == "command" && property.Value.ValueKind == JsonValueKind.String)
                        {
                            commands.Add(property.Value.GetString()!);
                        }
                        else
                        {
                            Collect(property.Value, commands);
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Collect(item, commands);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The hook commands that do not name their script from the root of the
    /// checkout, or name one that does not exist.
    /// </summary>
    private static IReadOnlyList<string> UnreachableHooks(string settings) => HookCommands(settings)
        .Where(command =>
        {
            var start = command.IndexOf(ProjectDirectory, StringComparison.Ordinal);
            if (start < 0)
            {
                return true;
            }

            var script = command[(start + ProjectDirectory.Length)..].Split('"', ' ')[0];
            return !File.Exists(Path.Combine(RepositoryLayout.Root, script.Replace('/', Path.DirectorySeparatorChar)));
        })
        .ToList();
}
