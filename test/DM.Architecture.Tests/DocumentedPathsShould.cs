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
    private static readonly Regex RepositoryPath = new(
        @"(?:^|[^\w./\\-])((?:docker|src|test|docs|\.claude)/[\w./-]+\.(?:yml|yaml|json|cs|ts|tsx|vue|md|props|sh|js|cjs|service|Dockerfile))",
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
}
