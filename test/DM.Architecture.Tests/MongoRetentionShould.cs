using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every Mongo collection says how long it keeps what it is given.
/// </summary>
/// <remarks>
/// Two collections were given a term with the reason written next to it. The
/// third stream in the database - a document per like, comment, invitation and
/// change of status, kept forever - was given none, and nothing distinguished
/// that from a decision. The silence is the defect: it reads the same whether
/// the term was weighed and rejected or never thought about, and the cost of it
/// grows on its own, because the list counts the whole history of the user on
/// every page.
///
/// So a collection either declares a term or is named here as state bounded by
/// what it belongs to. Asserted on the text of the declaration, because the
/// index set is asserted against a live server at startup and there is none
/// under test.
/// </remarks>
public class MongoRetentionShould
{
    private const string Initializer =
        "src/DM.Infrastructure.Persistence/MongoIntegration/MongoIndexInitializer.cs";

    /// <summary>The bootstrap script for a fresh data volume, which mirrors the set.</summary>
    private const string InitScript = "docker/mongo-init.js";

    /// <summary>Start of one collection's block of index declarations.</summary>
    private const string CollectionBlock = "await Assert(client.GetCollection<";

    /// <summary>A term, as the index helper takes it.</summary>
    private const string DeclaresATerm = "expireAfter:";

    /// <summary>The same term in the bootstrap script.</summary>
    private const string ScriptTerm = "expireAfterSeconds:";

    /// <summary>
    /// State rather than stream: one document per user, per session, per poll,
    /// per schema, per roll of a post. Each is bounded by the entity it belongs
    /// to and goes when that goes, so a term here would only delete something
    /// still in use.
    /// </summary>
    private static readonly string[] Bounded =
    {
        "DbUnreadCounter", "DbUserSession", "DbUserSettings",
        "DbPoll", "DbAttributeSchema", "DbDiceRoll",
    };

    [Fact]
    public void DeclareATermForEveryCollectionThatIsAStream()
    {
        var blocks = Blocks();

        blocks.Should().HaveCountGreaterOrEqualTo(8,
            "a rule that matches nothing passes: the initializer declares the indexes " +
            "of every collection in the database");

        blocks
            .Where(block => !Bounded.Contains(block.Entity, StringComparer.Ordinal))
            .Where(block => !block.Text.Contains(DeclaresATerm, StringComparison.Ordinal))
            .Select(block => block.Entity)
            .Should().BeEmpty(
                "a collection that gains a document per user action and loses none grows " +
                "without any bound, and nothing distinguishes that from a decision to " +
                "keep it all");
    }

    /// <summary>
    /// The exemption has to keep naming a collection that exists, or a rename
    /// turns it into a hole nobody sees.
    /// </summary>
    [Fact]
    public void KeepTheListOfBoundedCollectionsHonest() =>
        Bounded.Except(Blocks().Select(block => block.Entity), StringComparer.Ordinal)
            .Should().BeEmpty(
                "an exemption for a collection the initializer no longer declares guards " +
                "nothing and outlives its reason");

    [Fact]
    public void MirrorEveryTermInTheBootstrapScript() =>
        Occurrences(Read(InitScript), ScriptTerm)
            .Should().Be(Occurrences(Read(Initializer), DeclaresATerm),
                "the script runs once on an empty volume and the initializer runs on every " +
                "start, so a term declared in one and not the other makes the retention of " +
                "a database depend on which of the two created it");

    private static (string Entity, string Text)[] Blocks() => Read(Initializer)
        .Split(CollectionBlock, StringSplitOptions.None)
        .Skip(1)
        .Select(block => (Entity: block[..block.IndexOf('>')], Text: block))
        .ToArray();

    private static int Occurrences(string text, string token) =>
        text.Split(token).Length - 1;

    private static string Read(string relative)
    {
        var path = Path.Combine(
            RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(path).Should().BeTrue($"{relative} must exist at {path}");
        return File.ReadAllText(path);
    }

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
