using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A document that names a file this repository does not contain sends the
/// reader looking for something nobody wrote.
/// </summary>
/// <remarks>
/// The monitoring guide handed the operator a receiver configuration to fill in
/// and gave its path. There was no such file, no service to read it and no
/// routing section pointing at one, so alert rules were evaluated and delivered
/// nowhere — while the instruction made that look like a configured system.
/// Whether prose about behaviour is true cannot be checked mechanically; whether
/// a path exists can, and the path is what the reader acts on first.
/// </remarks>
public class DocumentedPathsShould
{
    /// <summary>
    /// Repo-relative references only: a path starting at one of the top-level
    /// directories is a claim about this tree. Absolute, remote and
    /// dot-relative paths belong to examples and are not.
    /// </summary>
    /// <remarks>
    /// The extensions are listed rather than left open because this tree documents
    /// files it deliberately does not carry - the environment files and the
    /// credentials an installer generates - and a pattern accepting any extension
    /// reads those as broken promises. The price of the list is that a kind absent
    /// from it is not checked at all, and stylesheets were: seven paths under an
    /// src/assets directory this repository has never had sat in the interface
    /// conventions while this gate ran green over them.
    ///
    /// The trailing lookahead is what makes the order of the alternatives stop
    /// mattering. Without it ".css" matches "cs" and ".tsx" matches "ts", and the
    /// gate then goes looking for a file whose name it truncated itself.
    /// </remarks>
    private static readonly Regex RepositoryPath = new(
        @"(?:^|[^\w./\\-])((?:docker|src|test|docs|\.claude)/[\w./-]+\.(?:yml|yaml|json|cs|ts|tsx|vue|sass|scss|css|md|props|sh|js|cjs|service|Dockerfile))(?![\w])",
        RegexOptions.Compiled);

    /// <summary>
    /// Walks up from the test binary to the repository root. The documents are
    /// not copied to the output directory, and copying them would let this
    /// assert against a stale snapshot.
    /// </summary>
    private static DirectoryInfo RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!;
        }
    }

    [Fact]
    public void NameOnlyFilesThatExist()
    {
        var root = RepositoryRoot;
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
    /// A stylesheet path is a reference like any other. The last case is the
    /// spelling the interface conventions carried for months: a file that exists,
    /// named under a directory that does not.
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
