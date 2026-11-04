using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A permission check asks a question some resolver can answer.
/// </summary>
/// <remarks>
/// The manager picks a resolver by the runtime type of the target and refuses when none matches,
/// which reads as a permission refusal. Asserted on the sources: both types are legal arguments.
/// </remarks>
public class IntentionTargetsShould
{
    /// <summary>Reads the room without its game, so no room rule can be answered from it.</summary>
    private const string FlatRoomSource = ".GetAsync(";

    private static readonly Regex RoomIntentionCall = new(
        @"(?:ThrowIfForbidden|IsAllowed)\(\s*RoomIntention\.\w+\s*,\s*(?<target>\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex WriteIntentionCall = new(
        @"ThrowIfForbidden\(\s*\w*Intention\.(?<verb>Create|Update|Delete)\w*",
        RegexOptions.Compiled);

    /// <summary>A method declaration, captured by name.</summary>
    private static readonly Regex MethodDeclaration = new(
        @"^\s*(?:public|private|internal|protected)[\w\s<>,\[\]\(\)\?]*?\s(?<name>\w+)\s*\(",
        RegexOptions.Compiled);



    /// <summary>
    /// Name of the method the given line sits in, by walking back to the nearest declaration; it fails
    /// towards reporting nothing.
    /// </summary>
    private static string EnclosingMethod(string[] lines, int at)
    {
        for (var i = at; i >= 0; i--)
        {
            var match = MethodDeclaration.Match(lines[i]);
            if (match.Success && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                return match.Groups["name"].Value;
            }
        }

        return string.Empty;
    }

    [Fact]
    public void NeverAskARoomRuleWithoutTheGame()
    {
        var offenders = new List<string>();

        foreach (var path in RepositoryFiles.ProductionSources())
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var call = RoomIntentionCall.Match(lines[i]);
                if (!call.Success) continue;

                var target = call.Groups["target"].Value;
                // Where the target came from: the nearest assignment above it.
                var assignment = new Regex($@"\b{Regex.Escape(target)}\s*=\s*await\s+(?<origin>[\w.]+)\(");
                for (var back = i - 1; back >= 0 && back > i - 30; back--)
                {
                    var found = assignment.Match(lines[back]);
                    if (!found.Success) continue;

                    if (lines[back].Contains(FlatRoomSource, StringComparison.Ordinal))
                    {
                        offenders.Add(
                            $"{Path.GetFileName(path)}:{i + 1} asks {call.Value.Split('(')[1].Split(',')[0]} " +
                            $"with {target} from {found.Groups["origin"].Value}");
                    }

                    break;
                }
            }
        }

        offenders.Should().BeEmpty(
            "the resolver for a room rule takes the projection that carries the game; " +
            "handed the flat one the manager finds nothing, logs \"no matching resolver\" " +
            "and refuses every caller, which is indistinguishable from a real refusal");
    }

    [Fact]
    public void NeverGateAReadWithAWriteIntention()
    {
        var offenders = new List<string>();

        foreach (var path in RepositoryFiles.ProductionSources())
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var write = WriteIntentionCall.Match(lines[i]);
                if (!write.Success) continue;

                var method = EnclosingMethod(lines, i);
                if (!method.StartsWith("Get", StringComparison.Ordinal)) continue;

                offenders.Add($"{Path.GetFileName(path)}:{i + 1} {method} gated by {write.Groups["verb"].Value}");
            }
        }

        offenders.Should().BeEmpty(
            "a reader is not asking to write, and gating a read on a writing right " +
            "refuses everybody who may only read");
    }
}
