using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// No document grows past 80 KB, and the ones already past it on the day the
/// limit arrived are held at the size they had then.
/// </summary>
/// <remarks>
/// A document is read whole by every agent that is pointed at it, and past this
/// size one read no longer holds it: the reader takes a part and acts on what it
/// did not see. The cure is the one the conventions already prescribe, links
/// instead of copies and a section moved into the document that owns it.
///
/// Same contract as SizeRatchetShould: each record is the measured size in bytes
/// and is checked for equality from both ends. A document past the limit with no
/// record fails, a record that has grown fails, and so does one that has shrunk
/// or has nothing past the limit behind it any more. The register only shrinks;
/// a new record takes the owner's word.
///
/// Measured over the files a commit would carry, with line ends counted as one
/// byte, so that a checkout with Windows line ends measures what the repository
/// holds.
/// </remarks>
public class DocumentSizeShould
{
    private const int Limit = 80 * 1000;

    /// <summary>A document past the limit, at its size.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Bytes">UTF-8 bytes with line ends counted as one.</param>
    private sealed record Recorded(string File, int Bytes);

    private static readonly Recorded[] Register =
    [
        new("docs/SURFACES.md", 104176),
        new("docs/architecture/BBCODE_RENDERING.md", 123508),
        new("docs/conventions/UI_STANDARDS.md", 194023),
        new("docs/plans/DIALOGS.md", 347701),
    ];

    /// <summary>
    /// Every document past the limit is recorded at its size, and every record has a
    /// document of that size behind it.
    /// </summary>
    [Fact]
    public void HoldEveryDocumentPastTheLimitAtItsRecordedSize()
    {
        var root = DM.Testing.RepositoryLayout.Root;
        var measured = RepositoryFiles.CommittableFiles()
            .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(path => (
                File: Path.GetRelativePath(root, path).Replace('\\', '/'),
                Bytes: Encoding.UTF8.GetByteCount(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal))))
            .Where(document => document.Bytes > Limit)
            .ToDictionary(document => document.File, document => document.Bytes, StringComparer.Ordinal);
        var recorded = Register.ToDictionary(entry => entry.File, entry => entry.Bytes, StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var file in recorded.Keys.Union(measured.Keys).Order(StringComparer.Ordinal))
        {
            var hasRecord = recorded.TryGetValue(file, out var record);
            var hasSize = measured.TryGetValue(file, out var size);
            if (hasRecord && hasSize && record == size)
            {
                continue;
            }

            problems.Add((hasRecord, hasSize) switch
            {
                (false, _) => $"{file}: {size} bytes is past {Limit} and has no record (new(\"{file}\", {size}),)",
                (_, false) => $"{file}: back within {Limit} bytes or gone: drop the record of {record}",
                _ when size > record => $"{file}: grew from {record} to {size} bytes",
                _ => $"{file}: shrank from {record} to {size} bytes: lower the record to {size}",
            });
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a document is held at its recorded size: move a section into the document " +
            "that owns it rather than record a new one (a new record needs the owner's " +
            "word), and lower a record the moment its document shrinks");
    }
}
