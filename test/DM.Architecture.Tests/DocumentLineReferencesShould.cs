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
/// A line number goes false after the first edit above it, and no rule can hold it to the tree.
/// Counted: "file.ext:line", "#Lnn" and "(:nn)" in prose and code spans, not in code blocks.
/// </remarks>
public class DocumentLineReferencesShould
{
    private static readonly Regex FileAndLine = new(
        @"[\w.-]+\.(?:cs|ts|tsx|vue|js|mjs|cjs|sass|scss|css|md|ps1|sh|yml|yaml|json|props|html)(?::\d+(?:-\d+)?|#L\d+)(?!\w)",
        RegexOptions.Compiled);

    private static readonly Regex BareLine = new(@"(?<=[(\s,]):\d+(?:-\d+)?(?=[),\s.])", RegexOptions.Compiled);

    [Fact]
    public void NameNoLineByItsNumber()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().NotBeEmpty("a rule that reads no document checks nothing");

        var referring = documents
            .Select(document => (document.Path, Count: document.Lines.Where(line => line.IsProse).Sum(line => Count(line.Text))))
            .Where(document => document.Count > 0)
            .Select(document => $"{document.Path}: {document.Count}");

        string.Join(Environment.NewLine, referring).Should().BeEmpty(
            "a line number goes stale with the first edit above it: name the file and the member or the " +
            "section instead");
    }

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
