using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Who removed a row and when is written in one place.
/// </summary>
/// <remarks>
/// ISoftDeletable declares the pair for twenty-five tables, and SoftDelete.Mark writes it
/// together with the flag precisely so that a delete path cannot set one and forget the
/// other. The class said as much about itself while eight methods - three in
/// BlogRepository, the award revoke, the notepad entry and three upload sweeps - wrote the
/// columns by hand, which is a guarantee that holds only as long as everybody remembers
/// it. A promise nothing enforces is worse than no promise: the reader of the next delete
/// path believes it.
///
/// The rule is over the two audit columns rather than over the flag alone. A table that
/// carries neither an author nor a moment (IRemovable without ISoftDeletable: a warning, a
/// moderator's note) has nothing to forget, and a rule that also covered those would need
/// a list of exceptions - the same convention-by-agreement this one replaces. The columns
/// exist only where the audit exists, so naming them names exactly the paths that can lose
/// it.
/// </remarks>
public class SoftDeleteAuditShould
{
    /// <summary>
    /// An assignment to one of the audit columns. A comparison is not one, hence the
    /// lookahead, and a declaration is not one either, hence the leading dot.
    /// </summary>
    private static readonly Regex AuditWrite = new(
        @"\.(?<column>DeletedByUserId|DeletedUtc)\s*=(?!=)", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string PersistenceRoot => Path.Combine(
        RepositoryRoot, "src", "DM.Infrastructure.Persistence");

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
                    $"{Path.GetRelativePath(RepositoryRoot, file)}:{line}: {write.Groups["column"].Value}");
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
        // The rule above is a scan, and a scan that stops matching passes in silence. This
        // is the fixture that says it still recognises both the shape it looks for and the
        // shape it must leave alone.
        const string byHand = @"
        blog.IsRemoved = true;
        blog.DeletedByUserId = deletedByUserId;
        blog.DeletedUtc = _dateTimeProvider.Now;";
        const string marked = @"
        SoftDelete.Mark(blog, deletedByUserId, _dateTimeProvider.Now);
        if (blog.DeletedUtc == null) return;";

        AuditWrite.IsMatch(byHand).Should().BeTrue("this is the shape the rule exists for");
        AuditWrite.IsMatch(marked).Should().BeFalse(
            "a call through Mark writes nothing itself, and a comparison is not an assignment");
    }

    [Fact]
    public void FindTheFilesToSearch()
    {
        SourceFiles().Should().HaveCountGreaterThan(100,
            "a file walk that stops matching turns the rule above green by checking nothing");
        File.Exists(MarkFile).Should().BeTrue(
            "the rule exempts one file, and a renamed one would exempt nothing while the " +
            "assembly kept writing the columns somewhere else");
    }
}
