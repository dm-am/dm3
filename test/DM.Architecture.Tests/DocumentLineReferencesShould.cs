using System;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A hand-written document points at code by the name of a file, a member or a
/// section, never by a line number.
/// </summary>
/// <remarks>
/// A line number is true on the day it is written and false after the first edit
/// above it, and nothing tells the reader which day that was: the reference still
/// reads as precise. A design document showed what that costs. Of eight
/// references of the form file:line checked against the tree, five pointed at the
/// wrong lines. A name keeps pointing where it pointed, and DocumentReferencesShould
/// and DocumentedPathsShould can hold a name to the tree, which no rule can do for
/// a number.
///
/// Two spellings are counted: a file with its extension followed by a line or a
/// range (BBCodeEditor.vue:1340-1348, Dialog.vue#L37), and the short one that
/// leans on a file named just before it, a colon and a number standing alone in
/// parentheses or a list ("(:132)", "(:62-67, :114-120)"). A code span counts:
/// that is where these references are usually written. A block of code does not,
/// since a stack trace quoted as an example carries its lines legitimately.
///
/// Recorded per document at its count (see <see cref="HandwrittenDocuments"/> for
/// the contract of the register): the cleanup replaces each number with the name
/// of what it pointed at.
/// </remarks>
public class DocumentLineReferencesShould
{
    /// <summary>A file with its extension, then a line or a range of lines.</summary>
    private static readonly Regex FileAndLine = new(
        @"[\w.-]+\.(?:cs|ts|tsx|vue|js|mjs|cjs|sass|scss|css|md|ps1|sh|yml|yaml|json|props|html)(?::\d+(?:-\d+)?|#L\d+)(?!\w)",
        RegexOptions.Compiled);

    /// <summary>A colon and a line standing alone, the file named before it.</summary>
    private static readonly Regex BareLine = new(@"(?<=[(\s,]):\d+(?:-\d+)?(?=[),\s.])", RegexOptions.Compiled);

    /// <summary>A document holding line references, at their count.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Count">How many references the document holds.</param>
    private sealed record Recorded(string File, int Count);

    private static readonly Recorded[] Register = [];

    [Fact]
    public void HoldEveryLineReferenceAtItsRecordedCount()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().HaveCountGreaterThan(30, "a rule that reads a handful of documents checks little");

        var measured = documents
            .Select(document => (document.Path, Count: document.Lines.Where(line => line.IsProse).Sum(line => Count(line.Text))))
            .Where(document => document.Count > 0)
            .ToDictionary(document => document.Path, document => document.Count, StringComparer.Ordinal);

        var problems = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => entry.File, entry => entry.Count, StringComparer.Ordinal),
            measured,
            (file, count) => $"new(\"{file}\", {count}),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a line number goes stale with the first edit above it: name the file and the member or the " +
            "section instead, and lower the record the moment a document loses one");
    }

    /// <summary>What counts as a reference to a line.</summary>
    [Theory]
    [InlineData("(`shared/ui/BBCodeEditor/BBCodeEditor.vue:1340-1348`)", 1)]
    [InlineData("see Dialog.vue#L37 and Dialog.vue:12", 2)]
    [InlineData("поля 24 (:132), рамкой (:133-137)", 2)]
    [InlineData("(:308-325, :410-412)", 2)]
    [InlineData("http://localhost:5173/users и 12:30", 0)]
    [InlineData("Dialog.vue, метод open", 0)]
    public void CountAReferenceToALine(string line, int count) => Count(line).Should().Be(count);

    private static int Count(string line) => FileAndLine.Matches(line).Count + BareLine.Matches(line).Count;
}
