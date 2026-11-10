using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>Apart from <see cref="AgentPermissionsShould"/>: the two fail for unrelated reasons.</remarks>
public class AgentHookPathsShould
{

    // The variable the agent sets to the root of the checkout before it runs a hook.
    private const string ProjectDirectory = "$CLAUDE_PROJECT_DIR/";

    // A hook runs in the agent's current directory, so a path relative to the root is not found
    // below it.
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
