using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A document that names a file this repository does not contain sends the
/// reader looking for something nobody wrote.
/// </summary>
/// <remarks>Whether prose is true cannot be checked; whether a path exists can, and it is acted on first.</remarks>
public class DocumentedPathsShould
{
    /// <summary>
    /// Repo-relative references only: a path starting at one of the top-level
    /// directories is a claim about this tree. Absolute, remote and
    /// dot-relative paths belong to examples and are not.
    /// </summary>
    /// <remarks>
    /// The extensions are listed: the tree documents files it deliberately does not carry. The
    /// trailing lookahead keeps ".css" from matching "cs".
    /// </remarks>
    private static readonly Regex RepositoryPath = new(
        @"(?:^|[^\w./\\-])((?:docker|src|test|docs|\.claude)/[\w./-]+\.(?:yml|yaml|json|cs|ts|tsx|vue|sass|scss|css|md|props|sh|js|cjs|service|Dockerfile))(?![\w])",
        RegexOptions.Compiled);

    /// <summary>
    /// Paths the repository deliberately never carries, and which the guides
    /// still have to name because that is where the reader has to put something.
    /// </summary>
    /// <remarks>Written out one by one, not as "anything gitignored", so each exemption is a decision.</remarks>
    private static readonly string[] NeverTracked =
    [
        ".claude/settings.local.json",
    ];


    [Fact]
    public void NameOnlyFilesThatExist()
    {
        var root = RepositoryLayout.RootDirectory;
        var documents = Directory.GetFiles(
            Path.Combine(root.FullName, "docs"), "*.md", SearchOption.AllDirectories);

        documents.Should().NotBeEmpty("the conventions and guides live under docs/");

        var missing = new List<string>();
        var references = 0;

        foreach (var document in documents)
        {
            foreach (Match match in RepositoryPath.Matches(File.ReadAllText(document)))
            {
                var reference = match.Groups[1].Value;
                if (NeverTracked.Contains(reference)) continue;

                references++;

                var target = Path.Combine(
                    root.FullName, reference.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(target))
                {
                    missing.Add(Path.GetRelativePath(root.FullName, document) + " -> " + reference);
                }
            }
        }

        references.Should().BeGreaterThan(0, "the documents point at files in this repository");
        missing.Should().BeEmpty(
            "a documented path is a promise the tree has to keep, and the reader has " +
            "no other way to find out that it does not");
    }

    /// <summary>
    /// A stylesheet path is a reference like any other, under a directory that exists too.
    /// </summary>
    [Theory]
    [InlineData(
        "`src/DM.Web.Client/src/assets/styles/_ZIndex.sass`",
        "src/DM.Web.Client/src/assets/styles/_ZIndex.sass")]
    [InlineData(
        "the theme variables live in src/DM.Web.Client/src/assets/styles/ThemeVariables.css",
        "src/DM.Web.Client/src/assets/styles/ThemeVariables.css")]
    [InlineData(
        "`src/assets/styles/_ZIndex.sass`",
        "src/assets/styles/_ZIndex.sass")]
    public void ReadAStylesheetPathAsAReference(string line, string reference) =>
        RepositoryPath.Match(line).Groups[1].Value.Should().Be(reference,
            "a stylesheet is named in a document the same way a component is, and the " +
            "reader goes looking for it the same way");
}
