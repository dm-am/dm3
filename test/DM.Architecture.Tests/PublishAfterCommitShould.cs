using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing is announced before the state it announces is durable.
/// </summary>
/// <remarks>
/// An event on the bus is read by consumers that send letters and bot messages,
/// and neither can be taken back. Publishing while the write is still in the
/// change tracker makes the announcement the reliable half and the record the
/// unreliable one: the reminder loop marked every stale pendency in memory,
/// published a reminder for each, and saved the batch afterwards, so a lost
/// connection to Postgres past that point sent the masters letters the database
/// holds no trace of - and twelve hours later sent them again, until one save
/// finally landed. Stopping the process between the loop and the save does the
/// same thing.
///
/// Asserted on the text, because the order of two awaits inside one method is
/// not something the type system or the IL has an opinion about, and both
/// services need a database and a broker to run at all.
/// </remarks>
public class PublishAfterCommitShould
{
    /// <summary>Commit of everything the change tracker holds.</summary>
    private const string Commit = "SaveChangesAsync(";

    /// <summary>Publication of a domain event, as every call site spells it.</summary>
    private const string Publish = "SendAsync(EventType.";

    private static string[] ServicesThatDoBoth => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .Where(path =>
        {
            var text = File.ReadAllText(path);
            return text.Contains(Commit, StringComparison.Ordinal) &&
                   text.Contains(Publish, StringComparison.Ordinal);
        })
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes. Two background services write and
    /// publish inside one pass, so finding fewer means the search strings went
    /// stale rather than that the tree is clean.
    /// </summary>
    [Fact]
    public void FindTheServicesThatWriteAndPublishInOnePass() =>
        ServicesThatDoBoth.Should().HaveCountGreaterOrEqualTo(2);

    [Fact]
    public void CommitBeforeTheFirstPublication() =>
        ServicesThatDoBoth
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.IndexOf(Publish, StringComparison.Ordinal) <
                       text.LastIndexOf(Commit, StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .Should().BeEmpty(
                "an event published while its own write is still pending is an " +
                "announcement that cannot be taken back about a state that may never " +
                "arrive, and the pass that runs next makes the same announcement again");

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
