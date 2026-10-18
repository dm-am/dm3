using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every hand-written document can be reached from README.md by following links.
/// </summary>
/// <remarks>
/// README is where a reader, a person or an agent, starts, and a document no
/// chain of links leads to is found only by somebody who already knows it is
/// there. Such a document stops being read, then stops being corrected, and keeps
/// stating whatever it stated on its last day while looking as current as the
/// rest.
///
/// A link is followed when it is relative and stands in prose, the way
/// DocumentReferencesShould reads links. It is followed through any markdown file
/// of the tree, the generated docs/STATUS.md included, and a link to a folder
/// reaches the README.md inside it, which is what the code host shows there.
///
/// The documents nothing leads to on the day the gate arrived are recorded by
/// name: the agents and skills under .claude, which the harness loads by itself,
/// and one plan. The register only shrinks (see <see cref="HandwrittenDocuments"/>):
/// linking a document from an index removes its record, and so does deleting it.
/// </remarks>
public class DocumentReachabilityShould
{
    private const string Start = "README.md";

    /// <summary>A markdown link: the target up to an optional anchor.</summary>
    private static readonly Regex Link = new(@"\]\((?<target>[^)\s#]*)(?:#[^)\s]+)?\)", RegexOptions.Compiled);

    /// <summary>Hand-written documents nothing leads to from README.md, by path from the root.</summary>
    private static readonly string[] Unreachable =
    [
        ".claude/agents/debugger.md",
        ".claude/agents/frontend-developer.md",
    ];

    [Fact]
    public void ReachEveryDocumentFromTheReadme()
    {
        var markdown = RepositoryFiles.CommittableFiles()
            .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(HandwrittenDocuments.Relative, path => path, StringComparer.Ordinal);
        markdown.Should().ContainKey(Start, "the walk starts at the README");

        var reached = new HashSet<string>(StringComparer.Ordinal) { Start };
        var queue = new Queue<string>([Start]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var target in LinksIn(current, File.ReadAllText(markdown[current])))
            {
                var document = markdown.ContainsKey(target) ? target : $"{target}/{Start}";
                if (markdown.ContainsKey(document) && reached.Add(document))
                {
                    queue.Enqueue(document);
                }
            }
        }

        reached.Should().HaveCountGreaterThan(20, "a walk that stops at the README reaches nothing");

        var measured = HandwrittenDocuments.All()
            .Select(document => document.Path)
            .Where(path => !reached.Contains(path))
            .ToDictionary(path => path, _ => 1, StringComparer.Ordinal);

        var problems = HandwrittenDocuments.Disagreements(
            Unreachable.ToDictionary(path => path, _ => 1, StringComparer.Ordinal),
            measured,
            (path, _) => $"\"{path}\",");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a document no chain of links from README.md leads to is found only by somebody who knows it " +
            "is there: link it from the index of its folder, and drop its record once it is reached");
    }

    /// <summary>
    /// The documents a file links to, as paths from the root: relative links in
    /// prose, resolved against the folder of the file.
    /// </summary>
    private static IEnumerable<string> LinksIn(string file, string text)
    {
        var folder = Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        foreach (var line in HandwrittenDocuments.Lines(text).Where(line => line.IsProse))
        {
            foreach (Match match in Link.Matches(line.Text))
            {
                var target = match.Groups["target"].Value;
                if (target.Length == 0 || target.Contains("://", StringComparison.Ordinal) ||
                    target.StartsWith('/') || target.StartsWith('<') ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var joined = Path.Combine(DM.Testing.RepositoryLayout.Root, folder, Uri.UnescapeDataString(target.Split('?')[0]));
                yield return HandwrittenDocuments.Relative(Path.GetFullPath(joined)).TrimEnd('/');
            }
        }
    }

    [Fact]
    public void ResolveALinkAgainstTheFolderOfItsDocument() =>
        LinksIn("docs/README.md", "[Гайды](guides/) и [стек](../STACK.md)")
            .Should().Equal("docs/guides", "STACK.md");
}
