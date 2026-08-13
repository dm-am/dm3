using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// What a search indexes and what it previews are the same string, and it is the
/// visible text rather than the body as written.
/// </summary>
/// <remarks>
/// Both used to read the raw body. The index tokenised markup — a tag name was a
/// word one could search for — and the preview cut the same raw string at a fixed
/// length, so the reader saw fragments of BBCode in the results, sometimes ending
/// in the middle of a tag. Neither failed anything: the query ran, the page
/// rendered, and the results were simply worse than they looked.
///
/// Read as text because the defect is which expression a projection names, and no
/// test that exercises a repository against a store can tell "the body" from "the
/// visible text of the body" — both are strings, and both are populated.
/// </remarks>
public class SearchPreviewSourceShould
{
    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string SearchDirectory => Path.Combine(RepositoryRoot,
        "src", "DM.Infrastructure.Persistence", "Repositories", "Search");

    private static string[] Repositories() => Directory
        .GetFiles(SearchDirectory, "*SearchRepository.cs");

    [Fact]
    public void BuildEveryPreviewFromTheProjectedText()
    {
        var repositories = Repositories();
        repositories.Should().NotBeEmpty("the walk has to find the search repositories");

        var assignments = repositories
            .SelectMany(path => Regex
                .Matches(SourceText.ReadCode(path), @"Snippet = ([^\r\n]+)")
                .Select(match => (File: Path.GetFileName(path), Source: match.Groups[1].Value.Trim())))
            .Where(assignment => !assignment.Source.StartsWith("SearchSnippet.", StringComparison.Ordinal))
            .ToList();

        assignments.Should().NotBeEmpty("the previews are assigned somewhere");

        foreach (var (file, source) in assignments)
        {
            source.Should().Contain("\"SearchText\"",
                $"{file} builds a preview out of {source}, which is the body as written: the " +
                "reader gets a fragment of markup, and it is not what the index was built from");
        }
    }

    /// <summary>
    /// The index reads the same projection, and reads nothing else.
    /// </summary>
    /// <remarks>
    /// A vector built from the raw body and a preview built from the projection is
    /// the older split with the halves swapped: the search would find words nobody
    /// can see and the preview would not contain them.
    /// </remarks>
    [Fact]
    public void BuildEveryVectorFromTheSameProjection()
    {
        var context = SourceText.ReadCode(Path.Combine(RepositoryRoot,
            "src", "DM.Infrastructure.Persistence", "DmDbContext.cs"));

        // One spelling of the expression and four uses of it, so the assertion is
        // over the spelling and over what each use hands it.
        var expression = Regex.Match(context, @"static string SearchVectorSql[\s\S]*?;");
        expression.Success.Should().BeTrue("the vector expression is written once");
        expression.Value.Should().Contain("coalesce",
            "a column that is never coalesced indexes nothing at all for a row that has none");

        var uses = Regex.Matches(context, @"SearchVectorSql\(""(\w+)""\)")
            .Select(match => match.Groups[1].Value)
            .ToList();

        uses.Should().HaveCountGreaterOrEqualTo(4,
            "four bodies are indexed, and a use the walk cannot see is one it says nothing about");
        uses.Should().OnlyContain(column => column == "SearchText",
            "a vector over the body as written tokenises markup, and the words it finds are " +
            "then absent from the preview beside them");

        Regex.Matches(context, @"HasComputedColumnSql\(\s*""to_tsvector")
            .Should().BeEmpty(
                "an expression written out at the call site is a fifth spelling of the rule, " +
                "and the one that stops agreeing with the other four says nothing about it");
    }
}
