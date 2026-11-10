using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// No C# file and no method grows past its limit, and the ones already past it
/// on the day the limits arrived are held at the length they had then.
/// </summary>
/// <remarks>
/// Size is what an agent pays for on every read: a file is read whole, comments
/// included, so lines are counted the way the reader meets them. A limit the tree
/// cannot meet on its first day gets switched off on its second, so the files and
/// methods past it are frozen rather than exempted, the way the client's
/// sizeRatchet.js freezes its own.
///
/// Every record is the measured length and is checked for equality from both
/// ends, as SuppressedRulesShould checks its list: past the limit with no record
/// fails, a record that has grown fails, and so does one that has shrunk or has
/// nothing past the limit behind it any more, because a record left above its
/// file is room for the file to grow back into.
///
/// The registers only shrink. A new record widens the exceptions and takes the
/// owner's word; the lawful move is to split the code, and a record may follow
/// a split onto one of the parts with a number no larger than before. Nothing
/// here can be switched off by a pragma: there is no analyzer to suppress.
///
/// Every record says in one line why its file or method stays whole: what makes
/// it long and why a cut would not read better. A record with no reason fails,
/// because one that cannot be given one is code to cut, not a length to hold.
///
/// Generated migrations are left out: EF writes them, and their length is the
/// model's, not anyone's prose.
/// </remarks>
public class SizeRatchetShould
{
    private const int SourceFileLimit = 600;

    /// <summary>
    /// Kept apart from the product's limit, because a test file carries its
    /// fixtures and cases in one place and the two may be tuned separately.
    /// </summary>
    private const int TestFileLimit = 600;

    private const int MethodLimit = 80;

    /// <param name="Lines">Measured length, a final newline adding no line.</param>
    /// <param name="Why">What makes the file long and why a cut would not read better.</param>
    private sealed record FileRecord(string File, int Lines, string Why);

    /// <param name="Method">The method's name, with #2, #3 for later overloads.</param>
    /// <param name="Why">What makes the method long and why a cut would not read better.</param>
    private sealed record MethodRecord(string File, string Method, int Lines, string Why);

    private static readonly FileRecord[] RecordedFiles = [];

    private static readonly MethodRecord[] RecordedMethods =
    [
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.ListPage.cs", "GetQuery", 106,
            "One guarded Where per field of UserFilter, each standing alone: parts would only move the predicates of one filter apart."),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/RemovedAuthorContentShould.cs", "SeedRemovedAuthorAsync", 116,
            "Seeds the one world every surface of the theory reads, a row per table, linked by the ids the World carries out: parts would hand those ids back and forth."),
    ];

    [Fact]
    public void HoldEveryFilePastItsLimitAtItsRecordedLength()
    {
        var measured = Sources()
            .Select(file => (File: file.Path, Lines: LineCount(file.Text), file.Limit))
            .Where(file => file.Lines > file.Limit)
            .ToDictionary(file => file.File, file => file.Lines, StringComparer.Ordinal);

        var problems = Disagreements(
            RecordedFiles.ToDictionary(entry => entry.File, entry => entry.Lines, StringComparer.Ordinal),
            measured,
            key => $"new(\"{key}\", {measured.GetValueOrDefault(key)}, \"why\"),");

        // Joined, so that the message lists every one rather than the first few.
        string.Join(Environment.NewLine, problems).Should().BeEmpty(
                "a file is held at its recorded length: split a file past the limit rather " +
                "than record it (a new record needs the owner's word), and lower a record " +
                "the moment its file shrinks; " + HowToCut);
    }

    [Fact]
    public void HoldEveryMethodPastItsLimitAtItsRecordedLength()
    {
        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Sources())
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var span in SourceText.MemberSpans(file.Text))
            {
                var count = seen[span.Name] = seen.GetValueOrDefault(span.Name) + 1;
                var name = count == 1 ? span.Name : $"{span.Name}#{count}";
                if (span.Lines > MethodLimit)
                {
                    measured[Key(file.Path, name)] = span.Lines;
                }
            }
        }

        var problems = Disagreements(
            RecordedMethods.ToDictionary(entry => Key(entry.File, entry.Method), entry => entry.Lines, StringComparer.Ordinal),
            measured,
            key => $"new(\"{key.Replace(" :: ", "\", \"", StringComparison.Ordinal)}\", {measured.GetValueOrDefault(key)}, \"why\"),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
                "a method is held at its recorded length: split a method past the limit " +
                "rather than record it (a new record needs the owner's word), and lower a " +
                "record the moment its method shrinks; " + HowToCut);
    }

    [Fact]
    public void GiveEveryRecordItsReason()
    {
        var unexplained = RecordedFiles
            .Where(entry => string.IsNullOrWhiteSpace(entry.Why))
            .Select(entry => entry.File)
            .Concat(RecordedMethods
                .Where(entry => string.IsNullOrWhiteSpace(entry.Why))
                .Select(entry => Key(entry.File, entry.Method)));

        string.Join(Environment.NewLine, unexplained).Should().BeEmpty(
                "a record holds a length only with its reason, in one line: what makes the " +
                "code long and why a cut would not read better; with none to give, " + HowToCut);
    }

    private const string HowToCut =
        "cut by meaning, each part named after what it does, never by size alone";

    private static string Key(string file, string method) => $"{file} :: {method}";

    /// <remarks>
    /// Asked of git, as DocumentSizeShould asks: a walk of the disk also reads
    /// what the ignore list keeps out, such as the dependencies of the client,
    /// so a run here would measure files that a run in CI never sees.
    /// </remarks>
    private static IEnumerable<(string Path, string Text, int Limit)> Sources()
    {
        var root = DM.Testing.RepositoryLayout.Root;
        foreach (var path in RepositoryFiles.CommittableFiles())
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            int? limit = relative.StartsWith("src/", StringComparison.Ordinal) ? SourceFileLimit
                : relative.StartsWith("test/", StringComparison.Ordinal) ? TestFileLimit
                : null;
            if (limit is null
                || !relative.EndsWith(".cs", StringComparison.Ordinal)
                || relative.Contains("/Migrations/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(path), limit.Value);
        }
    }

    /// <summary>Counted the way ESLint's max-lines counts: a final newline adds no line.</summary>
    private static int LineCount(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines.Length > 1 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
    }

    private static List<string> Disagreements(
        IReadOnlyDictionary<string, int> recorded,
        IReadOnlyDictionary<string, int> measured,
        Func<string, string> entryFor)
    {
        var problems = new List<string>();
        foreach (var key in recorded.Keys.Union(measured.Keys).Order(StringComparer.Ordinal))
        {
            var hasRecord = recorded.TryGetValue(key, out var record);
            var hasSize = measured.TryGetValue(key, out var size);
            if (hasRecord && hasSize && record == size)
            {
                continue;
            }

            problems.Add((hasRecord, hasSize) switch
            {
                (false, _) => $"{key}: {size} lines is past the limit and has no record ({entryFor(key)}): {HowToCut}",
                (_, false) => $"{key}: back within the limit or gone: drop the record of {record}",
                _ when size > record => $"{key}: grew from {record} to {size} lines: {HowToCut}",
                _ => $"{key}: shrank from {record} to {size} lines: lower the record to {size}",
            });
        }

        return problems;
    }
}
