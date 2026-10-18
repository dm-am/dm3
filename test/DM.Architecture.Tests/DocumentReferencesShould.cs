using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A reference to a document reaches what it names: the section a comment cites,
/// the file a relative link points at, and the heading an anchor jumps to.
/// </summary>
/// <remarks>
/// The paths a document names from the root of the tree are held by
/// DocumentedPathsShould; these are the other three ways code and documents cite
/// each other: a comment citing a section by its title (UI_STANDARDS,
/// "Всплывающие сообщения"), a relative link between documents, and an anchor
/// into a heading. Renaming a heading or cutting a large document into parts
/// breaks all three, and a reference that stops resolving still reads as an
/// explanation: the reader goes looking and finds nothing.
///
/// A section is found where a reader would look for it: a heading, a line that
/// opens with a bold lead, or a row of a table. A document cut into an index and
/// its parts keeps the parts in a folder of its own name, and a section is
/// looked for in the parts the index links to as well.
/// </remarks>
public class DocumentReferencesShould
{
    /// <summary>The trees whose files cite documents.</summary>
    private static readonly string[] CitingTrees = ["src", "test", "scripts", "docker", ".claude", ".github", "docs"];

    private static readonly string[] CitingExtensions =
    [
        ".cs", ".ts", ".vue", ".sass", ".js", ".cjs", ".mjs", ".sh", ".ps1", ".yml", ".yaml", ".md",
    ];

    /// <summary>
    /// A markdown link: the target up to an optional anchor.
    /// </summary>
    private static readonly Regex Link = new(@"\]\((?<target>[^)\s#]*)(?:#(?<anchor>[^)\s]+))?\)", RegexOptions.Compiled);

    /// <summary>A fence that opens or closes a block of code in a document.</summary>
    private static readonly Regex Fence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    // ── Sections cited by title ──

    [Fact]
    public void FindEverySectionItsCitationsName()
    {
        var documents = Documents();
        var citations = Citations(documents.Keys).ToList();

        citations.Should().HaveCountGreaterThan(50,
            "the tree cites some seventy sections by title, and a walk that finds a handful checks little");

        // Counted rather than asked to be empty: a failed count prints every
        // entry, a failed emptiness check only the first.
        citations
            .Where(citation => !Resolves(documents[citation.Document], citation.Section))
            .Select(citation => $"{citation.File}: {citation.Document}, \"{citation.Section}\"")
            .Should().HaveCount(0,
                "a section cited by a title that no heading, bold lead or table row of the document " +
                "carries any more sends the reader looking for something that was renamed or moved");
    }

    /// <summary>
    /// What counts as a citation, and what a section can be found as.
    /// </summary>
    [Theory]
    [InlineData("// (UI_STANDARDS, \"Шапка\")", "## Шапка", true)]
    [InlineData("// UI_STANDARDS.md, section \"Шапка\"", "## Шапка и меню", true)]
    [InlineData("// (UI_STANDARDS, \"Ожидание запроса\")", "| Ожидание запроса (`:loading`) | Вид |", true)]
    [InlineData("// (API_DESIGN.md: \"Envelope<T> - стандарт\")", "> **Правило:** `Envelope<T>` — стандарт.", true)]
    [InlineData("// (UI_STANDARDS, \"Шапка\")", "Про шапку и меню в прозе.", false)]
    [InlineData("// (UI_STANDARDS, \"Шапка\")", "## Подвал", false)]
    public void ResolveACitationTheWayAReaderLooks(string source, string documentLine, bool resolves)
    {
        var citation = CitationsIn("probe.ts", source, ["UI_STANDARDS", "API_DESIGN"]).Single();

        Resolves([documentLine], citation.Section).Should().Be(resolves);
    }

    /// <summary>
    /// A file reading a document by its name is not citing it: the quoted string is an
    /// argument, and an argument does not open with a capital the way a title does.
    /// </summary>
    [Fact]
    public void LeaveACallThatNamesADocumentAlone() =>
        CitationsIn("probe.ts", "readFileSync(SURFACES, \"utf8\")", ["SURFACES"])
            .Should().BeEmpty();

    /// <summary>
    /// A document cut into an index and its parts still answers for the sections it had.
    /// </summary>
    [Fact]
    public void FollowTheIndexOfADocumentIntoItsParts()
    {
        var directory = Directory.CreateTempSubdirectory("dm-doc-index-");
        try
        {
            var parts = Directory.CreateDirectory(Path.Combine(directory.FullName, "UI_STANDARDS"));
            File.WriteAllText(Path.Combine(parts.FullName, "header.md"), "# Шапка\n\nТекст.\n");
            File.WriteAllText(Path.Combine(directory.FullName, "DIALOGS.md"), "# Подвал\n");
            var index = Path.Combine(directory.FullName, "UI_STANDARDS.md");
            File.WriteAllText(index,
                "# Стандарты\n\n- [Шапка и меню](./UI_STANDARDS/header.md)\n- [Окна](./DIALOGS.md)\n");

            Resolves(SearchedLines(index), "Шапка").Should().BeTrue(
                "the index names the part the section moved into");
            Resolves(SearchedLines(index), "Подвал").Should().BeFalse(
                "a document the index only mentions is not a part of it");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // ── Relative links ──

    [Fact]
    public void PointEveryRelativeLinkAtAFileThatExists()
    {
        var links = MarkdownFiles()
            .SelectMany(file => LinksIn(file).Select(link => (File: file, Link: link)))
            .Where(entry => IsRelative(entry.Link.Target))
            .ToList();

        links.Should().HaveCountGreaterThan(100,
            "the documents link to each other some two hundred and fifty times");

        links
            .Where(entry => !Exists(Resolve(entry.File, entry.Link.Target)))
            .Select(entry => $"{Relative(entry.File)} -> {entry.Link.Target}")
            .Should().HaveCount(0,
                "a relative link whose file is gone leads the reader nowhere");
    }

    // ── Anchors ──

    [Fact]
    public void PointEveryAnchorAtAHeadingThatExists()
    {
        var anchored = MarkdownFiles()
            .SelectMany(file => LinksIn(file).Select(link => (File: file, Link: link)))
            .Where(entry => entry.Link.Anchor is not null)
            .Where(entry => entry.Link.Target.Length == 0 ||
                            (IsRelative(entry.Link.Target) &&
                             entry.Link.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        anchored.Should().HaveCountGreaterThan(30, "the documents jump to each other's headings some seventy times");

        var anchorsByFile = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        anchored
            .Where(entry =>
            {
                var target = entry.Link.Target.Length == 0 ? entry.File : Resolve(entry.File, entry.Link.Target);
                if (!File.Exists(target))
                {
                    return true;
                }

                if (!anchorsByFile.TryGetValue(target, out var anchors))
                {
                    anchors = Anchors(File.ReadAllLines(target));
                    anchorsByFile[target] = anchors;
                }

                return !anchors.Contains(Uri.UnescapeDataString(entry.Link.Anchor!).ToLowerInvariant());
            })
            .Select(entry => $"{Relative(entry.File)} -> {entry.Link.Target}#{entry.Link.Anchor}")
            .Should().HaveCount(0,
                "an anchor whose heading was renamed opens the document at the top, and the reader " +
                "is left to find the section the link promised");
    }

    /// <summary>The anchors a heading answers to, by the rules the code host renders them with.</summary>
    [Theory]
    [InlineData("## Грамматика разметки", "грамматика-разметки")]
    [InlineData("### `Envelope<T>` и списки", "envelopet-и-списки")]
    [InlineData("## Realtime (SignalR)", "realtime-signalr")]
    [InlineData("## UI_STANDARDS: шапка", "ui_standards-шапка")]
    [InlineData("## Шаг 1 — сборка", "шаг-1--сборка")]
    public void NameAnAnchorAsTheHeadingIsRendered(string heading, string anchor) =>
        Anchors([heading]).Should().Contain(anchor);

    [Fact]
    public void NumberTheAnchorsOfHeadingsThatRepeat() =>
        Anchors(["## Пример", "text", "## Пример"]).Should().BeEquivalentTo(["пример", "пример-1"]);

    // ── Reading the tree ──

    private sealed record Citation(string File, string Document, string Section);

    private sealed record MarkdownLink(string Target, string? Anchor);

    /// <summary>The documents a citation can name, by name, with the lines a section is searched in.</summary>
    private static IReadOnlyDictionary<string, string[]> Documents() => FilesUnder("docs")
        .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Where(path => Regex.IsMatch(Path.GetFileNameWithoutExtension(path), "^[A-Z][A-Z0-9_]+$"))
        .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.SelectMany(SearchedLines).ToArray(), StringComparer.Ordinal);

    /// <summary>
    /// The lines of a document, and of its parts once it has been cut: the files
    /// it links to inside the folder that carries its name.
    /// </summary>
    /// <remarks>
    /// Only the parts, not every document it links to: a section found in a
    /// neighbour the document merely mentions would pass a citation whose own
    /// heading is gone.
    /// </remarks>
    private static IEnumerable<string> SearchedLines(string document)
    {
        var parts = Path.Combine(
            Path.GetDirectoryName(document)!, Path.GetFileNameWithoutExtension(document)) + Path.DirectorySeparatorChar;

        return File.ReadAllLines(document).Concat(
            LinksIn(document)
                .Where(link => IsRelative(link.Target) && link.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Select(link => Resolve(document, link.Target))
                .Where(path => path.StartsWith(parts, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(File.ReadAllLines));
    }

    private static IEnumerable<Citation> Citations(IEnumerable<string> documentNames)
    {
        var names = documentNames.ToArray();
        return CitingTrees
            .SelectMany(FilesUnder)
            .Where(path => CitingExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .SelectMany(path => CitationsIn(Relative(path), File.ReadAllText(path), names));
    }

    /// <summary>
    /// Citations of the form DOC, "Section" - with the extension, a colon or the
    /// word "section" or "раздел" allowed between. A title opens with a capital,
    /// which is what tells it from a string argument passed beside a name.
    /// </summary>
    private static IEnumerable<Citation> CitationsIn(string file, string text, IReadOnlyCollection<string> documents)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var pattern = new Regex(
            @"\b(?<doc>" + string.Join("|", documents.Select(Regex.Escape)) + @")(?:\.md)?[,:]?\s*" +
            @"(?:раздел\s*|section\s*)?""(?<section>\p{Lu}[^""\n]{2,79})""");

        return pattern.Matches(text)
            .Select(match => new Citation(file, match.Groups["doc"].Value, match.Groups["section"].Value))
            .ToList();
    }

    /// <summary>Whether a heading, a bold lead or a table row of the lines carries the section.</summary>
    private static bool Resolves(IEnumerable<string> lines, string section)
    {
        var wanted = Normalized(section);
        return lines
            .Where(line => Regex.IsMatch(line, @"^\s{0,3}#{1,6}\s") ||
                           Regex.IsMatch(line, @"^\s*(?:>\s*)?(?:[-*+]\s+|\d+\.\s+)?\*\*") ||
                           Regex.IsMatch(line, @"^\s*\|.*\|\s*$"))
            .Any(line => Normalized(line).Contains(wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The text a reader sees: no emphasis marks or code ticks, and one kind of dash,
    /// since a comment cannot always carry the long one a document typesets.
    /// </summary>
    private static string Normalized(string text) =>
        Regex.Replace(
            text.Replace("**", string.Empty, StringComparison.Ordinal)
                .Replace("`", string.Empty, StringComparison.Ordinal)
                .Replace('—', '-')
                .Replace('–', '-'),
            @"\s+", " ");

    private static IEnumerable<string> MarkdownFiles() => RepositoryFiles.CommittableFiles()
        .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));

    /// <summary>The links of a markdown file, those inside blocks of code left out.</summary>
    private static IEnumerable<MarkdownLink> LinksIn(string file)
    {
        var inCode = false;
        foreach (var line in File.ReadLines(file))
        {
            if (Fence.IsMatch(line))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                continue;
            }

            foreach (Match match in Link.Matches(line))
            {
                yield return new MarkdownLink(
                    match.Groups["target"].Value,
                    match.Groups["anchor"].Success ? match.Groups["anchor"].Value : null);
            }
        }
    }

    private static bool IsRelative(string target) =>
        target.Length > 0 &&
        !target.Contains("://", StringComparison.Ordinal) &&
        !target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
        !target.StartsWith('/') &&
        !target.StartsWith('<');

    private static string Resolve(string from, string target) =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(from)!,
            Uri.UnescapeDataString(target.Split('?')[0]).Replace('/', Path.DirectorySeparatorChar)));

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// The anchors the headings of a document answer to: the heading as rendered,
    /// lower case, with every character but letters, digits, the underscore, the
    /// space and the hyphen dropped, and spaces turned into hyphens; a repeated
    /// heading takes a number. Explicit anchors count too.
    /// </summary>
    private static IReadOnlySet<string> Anchors(IEnumerable<string> lines)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var inCode = false;

        foreach (var line in lines)
        {
            if (Fence.IsMatch(line))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                continue;
            }

            foreach (Match explicitAnchor in Regex.Matches(line, @"<a\s+(?:name|id)=""([^""]+)"""))
            {
                anchors.Add(explicitAnchor.Groups[1].Value.ToLowerInvariant());
            }

            var heading = Regex.Match(line, @"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$");
            if (!heading.Success)
            {
                continue;
            }

            var slug = Slug(heading.Groups[1].Value);
            if (seen.TryGetValue(slug, out var count))
            {
                seen[slug] = count + 1;
                slug = $"{slug}-{count + 1}";
            }
            else
            {
                seen[slug] = 0;
            }

            anchors.Add(slug);
        }

        return anchors;
    }

    private static string Slug(string heading)
    {
        // Between backticks the text is shown as written, so a tag is markup
        // only outside them: Envelope<T> in a code span keeps its T.
        var rendered = string.Concat(
            Regex.Replace(heading, @"\[([^\]]*)\]\([^)]*\)", "$1")
                .Split('`')
                .Select((segment, index) => index % 2 == 1
                    ? segment
                    : Regex.Replace(segment, "<[^>]+>", string.Empty).Replace("*", string.Empty, StringComparison.Ordinal)));

        var slug = new StringBuilder();
        foreach (var character in rendered.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-' ||
                char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                slug.Append(character);
            }
            else if (character == ' ')
            {
                slug.Append('-');
            }
        }

        return slug.ToString();
    }

    /// <summary>
    /// The files of the repository under one of its top directories (see
    /// <see cref="RepositoryFiles.CommittableFiles"/> for which files those are).
    /// </summary>
    private static IEnumerable<string> FilesUnder(string tree)
    {
        var prefix = Path.Combine(RepositoryRoot, tree) + Path.DirectorySeparatorChar;
        return RepositoryFiles.CommittableFiles()
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');
}
