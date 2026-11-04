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
/// Who removed a row and when is written in one place.
/// </summary>
/// <remarks>
/// SoftDelete.Mark and MarkWhere write the pair with the flag so no delete path forgets one.
/// Over the two audit columns, which exist only where the audit does.
/// </remarks>
public class SoftDeleteAuditShould
{
    /// <summary>
    /// A write to one of the audit columns, an assignment or a bulk SetProperty: not a comparison
    /// (the lookahead), not a declaration (the dot).
    /// </summary>
    private static readonly Regex AuditWrite = new(
        @"\.(?<column>DeletedByUserId|DeletedUtc)\s*=(?!=)|SetProperty\(\s*\w+\s*=>\s*\w+\.(?<column>DeletedByUserId|DeletedUtc)\b",
        RegexOptions.Compiled);

    private static string PersistenceRoot => Path.Combine(
        RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");

    /// <summary>The one file allowed to write the columns.</summary>
    private static string MarkFile => Path.Combine(
        PersistenceRoot, "RelationalStorage", "SoftDelete.cs");

    private static IEnumerable<string> SourceFiles() => Directory
        .EnumerateFiles(PersistenceRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        // Generated from the model: the migration writes the columns as a schema and the
        // snapshot as seed data, neither of which is a removal.
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
        .Where(path => !string.Equals(path, MarkFile, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void BeWrittenOnlyWhereTheFlagIsWrittenWithIt()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            // Comments out: a paragraph explaining the rule contains the shape it forbids.
            var code = SourceText.ReadCode(file);
            foreach (Match write in AuditWrite.Matches(code))
            {
                var line = code[..write.Index].Count(character => character == '\n') + 1;
                offenders.Add(
                    $"{Path.GetRelativePath(RepositoryLayout.Root, file)}:{line}: {write.Groups["column"].Value}");
            }
        }

        offenders.Should().BeEmpty(
            "a removal that fills the audit columns by hand is a removal that can leave one " +
            "of them empty, and moderation cannot answer 'who deleted this' from a column " +
            "that is right for some rows of the table and null for the rest");
    }

    [Fact]
    public void HaveASearchThatWouldFindOne()
    {
        // The rule above is a scan, and a scan that stops matching passes in silence.
        const string byHand = """
            blog.IsRemoved = true;
            blog.DeletedByUserId = deletedByUserId;
            blog.DeletedUtc = _dateTimeProvider.Now;
            """;
        const string inBulk = """
            rows.ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedUtc, now));
            """;
        const string marked = """
            SoftDelete.Mark(blog, deletedByUserId, _dateTimeProvider.Now);
            if (blog.DeletedUtc == null) return;
            """;

        AuditWrite.IsMatch(byHand).Should().BeTrue("this is the shape the rule exists for");
        AuditWrite.IsMatch(inBulk).Should().BeTrue("a bulk update writes the column as surely as an assignment");
        AuditWrite.IsMatch(marked).Should().BeFalse(
            "a call through Mark writes nothing itself, and a comparison is not an assignment");
    }

    [Fact]
    public void FindTheFilesToSearch() =>
        SourceFiles().Should().HaveCountGreaterThan(100,
            "a file walk that stops matching turns the rule above green by checking nothing");

    [Fact]
    public void KeepTheExemptedFileWhereTheRuleLooks()
    {
        File.Exists(MarkFile).Should().BeTrue(
            "the rule exempts one file, and a renamed one would exempt nothing while the " +
            "assembly kept writing the columns somewhere else");
    }
}
