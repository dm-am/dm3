using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    /// <summary>The bootstrap script for a fresh data volume.</summary>
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

    /// <summary>
    /// The index set is declared in one place, and the bootstrap script is not it.
    /// </summary>
    /// <remarks>
    /// The script used to hold a second copy of the whole set, and the two were kept
    /// mirrored by a rule counting terms on both sides. A copy that can only ever be
    /// applied to an empty volume drifts in one direction and is noticed by nobody,
    /// and mirroring it only meant maintaining the drift in step. The initializer
    /// asserts the set on every start of the API and of the seeder, which covers the
    /// fresh volume as well, so what is left to hold is that the copy stays gone.
    /// </remarks>
    [Fact]
    public void LeaveTheIndexSetToTheInitializerAlone()
    {
        var script = Read(InitScript);

        Occurrences(script, "createIndex(").Should().Be(0,
            "an index declared where it can only reach a brand-new volume is a copy " +
            "that drifts one way and tells nobody");
        Occurrences(script, ScriptTerm).Should().Be(0,
            "a retention term is part of the stored index descriptor, and the script " +
            "declares no indexes");
        Occurrences(Read(Initializer), DeclaresATerm).Should().BeGreaterThan(0,
            "the terms live in the initializer, and a rule matching nothing passes");
    }

    /// <summary>The self-summary at the end of the bootstrap script.</summary>
    private static readonly Regex TotalClaimed =
        new(@"Total:\s*(?<total>\d+)\s+indexes", RegexOptions.Compiled);

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

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
