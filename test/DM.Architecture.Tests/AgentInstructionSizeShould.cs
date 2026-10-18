using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The instructions an agent loads before it reads anything else do not grow:
/// each is held at its size in bytes, and the size only goes down.
/// </summary>
/// <remarks>
/// The session instructions under .claude are read into every session and every
/// agent, an agent's file into every run of that agent, and a skill into every
/// task that matches it, so a byte added there is paid on each of those reads
/// and crowds out the task the agent was started for. The documents themselves
/// say where a rule belongs instead: in the convention that owns it, by link.
///
/// The same contract as DocumentSizeShould, with the limit at today's size of
/// each file rather than one number for all of them: every file of the set has a
/// record, the record is its size, and the size is checked for equality from both
/// ends. A file that grew fails, one that shrank fails until its record is lowered
/// to match, and a new agent or skill has no record and fails until the owner
/// gives it one. Measured as DocumentSizeShould measures, UTF-8 with line ends
/// counted as one byte.
/// </remarks>
public class AgentInstructionSizeShould
{
    /// <summary>The instructions of the session, of each agent and of each skill.</summary>
    private static readonly Regex Instructions = new(
        @"^\.claude/(?:CLAUDE\.md|agents/[^/]+\.md|skills/[^/]+/SKILL\.md)$",
        RegexOptions.Compiled);

    /// <summary>A file of instructions, at its size.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Bytes">UTF-8 bytes with line ends counted as one.</param>
    private sealed record Recorded(string File, int Bytes);

    private static readonly Recorded[] Register =
    [
        new(".claude/CLAUDE.md", 8361),
        new(".claude/agents/code-reviewer.md", 5414),
        new(".claude/agents/debugger.md", 3147),
        new(".claude/agents/frontend-developer.md", 3251),
        new(".claude/skills/architecture-review/SKILL.md", 4615),
        new(".claude/skills/architecture/SKILL.md", 5594),
        new(".claude/skills/codex-issue-coordinator/SKILL.md", 10280),
        new(".claude/skills/design/SKILL.md", 6494),
        new(".claude/skills/html-doc/SKILL.md", 5151),
        new(".claude/skills/improve/SKILL.md", 1432),
        new(".claude/skills/plan/SKILL.md", 5097),
        new(".claude/skills/refactor-in-bounds/SKILL.md", 13086),
        new(".claude/skills/review/SKILL.md", 5953),
        new(".claude/skills/task-to-pr/SKILL.md", 4010),
        new(".claude/skills/test/SKILL.md", 2033),
    ];

    [Fact]
    public void HoldEveryFileOfInstructionsAtItsRecordedSize()
    {
        var measured = RepositoryFiles.CommittableFiles()
            .Select(path => (Path: HandwrittenDocuments.Relative(path), Full: path))
            .Where(file => Instructions.IsMatch(file.Path))
            .ToDictionary(
                file => file.Path,
                file => Encoding.UTF8.GetByteCount(System.IO.File.ReadAllText(file.Full).Replace("\r\n", "\n", StringComparison.Ordinal)),
                StringComparer.Ordinal);

        measured.Should().ContainKey(".claude/CLAUDE.md", "the instructions of the session are the first file of the set");

        var problems = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => entry.File, entry => entry.Bytes, StringComparer.Ordinal),
            measured,
            (file, bytes) => $"new(\"{file}\", {bytes}),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a byte added to the instructions is paid on every read of them: move the rule into the " +
            "convention that owns it and link it, lower the record the moment a file shrinks, and a new " +
            "agent or skill takes the owner's word for its record");
    }

    [Theory]
    [InlineData(".claude/CLAUDE.md", true)]
    [InlineData(".claude/agents/code-reviewer.md", true)]
    [InlineData(".claude/skills/review/SKILL.md", true)]
    [InlineData(".claude/skills/html-doc/tests/fixtures/prd.md", false)]
    [InlineData("docs/conventions/PROCESS.md", false)]
    public void TellAFileOfInstructions(string path, bool counted) =>
        Instructions.IsMatch(path).Should().Be(counted);
}
