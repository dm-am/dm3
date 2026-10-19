using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DM.Architecture.Tests;

/// <summary>
/// The documents a person writes, read the way the rules for their text read them:
/// by line, with each line knowing whether it is prose or a block of code.
/// </summary>
/// <remarks>
/// Every markdown file a commit carries, less docs/STATUS.md (a script's output) and an audit report at
/// the root. A register is held to the tree from both ends and only shrinks, as SizeRatchetShould's.
/// </remarks>
internal static class HandwrittenDocuments
{
    /// <summary>The one generated document, written by scripts/docs/build-status.mjs.</summary>
    public const string Generated = "docs/STATUS.md";

    private const string AuditReport = "AUDIT-";

    /// <summary>A fence that opens or closes a block of code, with the language it names.</summary>
    private static readonly Regex Fence = new(@"^\s*(?:```|~~~)\s*(?<language>[\w#+-]*)", RegexOptions.Compiled);

    /// <summary>A code span: one or more backticks, the text, the same number of backticks.</summary>
    private static readonly Regex CodeSpan = new(@"(`+)[^`\n]*?\1", RegexOptions.Compiled);

    private static readonly Lazy<IReadOnlyList<Document>> Read = new(ReadAll);

    /// <summary>A document, by its path from the root, with its lines.</summary>
    internal sealed record Document(string Path, string FullPath, IReadOnlyList<DocumentLine> Lines);

    /// <summary>
    /// One line of a document. <see cref="Language"/> is null for prose, empty for
    /// a block of code that names no language, and the name otherwise. The lines
    /// of the fences themselves are not listed.
    /// </summary>
    internal sealed record DocumentLine(int Number, string Text, string? Language)
    {
        public bool IsProse => Language is null;
    }

    public static IReadOnlyList<Document> All() => Read.Value;

    /// <summary>The text of a line with its code spans cut out.</summary>
    public static string WithoutCodeSpans(string line) => CodeSpan.Replace(line, " ");

    /// <summary>Path from the repository root, forward slashes.</summary>
    public static string Relative(string path) =>
        Path.GetRelativePath(DM.Testing.RepositoryLayout.Root, path).Replace('\\', '/');

    /// <summary>
    /// One line per key on which a register and the tree disagree, the register
    /// held to equality from both ends.
    /// </summary>
    /// <param name="recorded">The register, by key.</param>
    /// <param name="measured">What the tree holds, by key; zero counts left out.</param>
    /// <param name="entryFor">The register entry that records a key at its measure.</param>
    public static List<string> Disagreements(
        IReadOnlyDictionary<string, int> recorded,
        IReadOnlyDictionary<string, int> measured,
        Func<string, int, string> entryFor)
    {
        var problems = new List<string>();
        foreach (var key in recorded.Keys.Union(measured.Keys).Order(StringComparer.Ordinal))
        {
            var hasRecord = recorded.TryGetValue(key, out var record);
            var hasCount = measured.TryGetValue(key, out var count);
            if (hasRecord && hasCount && record == count)
            {
                continue;
            }

            problems.Add((hasRecord, hasCount) switch
            {
                (false, _) => $"{key}: {count} not in the register ({entryFor(key, count)})",
                (_, false) => $"{key}: none left: drop the record of {record}",
                _ when count > record => $"{key}: grew from {record} to {count}",
                _ => $"{key}: shrank from {record} to {count}: lower the record ({entryFor(key, count)})",
            });
        }

        return problems;
    }

    private static IReadOnlyList<Document> ReadAll() => RepositoryFiles.CommittableFiles()
        .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(path => (FullPath: path, Path: Relative(path)))
        .Where(file => file.Path != Generated &&
                       !file.Path.StartsWith(AuditReport, StringComparison.Ordinal))
        .OrderBy(file => file.Path, StringComparer.Ordinal)
        .Select(file => new Document(file.Path, file.FullPath, Lines(File.ReadAllText(file.FullPath))))
        .ToList();

    /// <summary>The lines of a text, each with the block of code it stands in, if any.</summary>
    internal static IReadOnlyList<DocumentLine> Lines(string text)
    {
        var lines = new List<DocumentLine>();
        string? language = null;
        var number = 0;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            number++;
            var fence = Fence.Match(line);
            if (fence.Success)
            {
                language = language is null ? fence.Groups["language"].Value : null;
                continue;
            }

            lines.Add(new DocumentLine(number, line, language));
        }

        return lines;
    }
}
