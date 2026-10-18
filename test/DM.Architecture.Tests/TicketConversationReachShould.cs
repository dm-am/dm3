using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The conversation of a ticket is read by the tickets and by nothing else.
/// </summary>
/// <remarks>
/// A ticket is a conversation with moderation about access recovery, personal data
/// or a complaint about a person, and its comments are kept in a table of their
/// own rather than among the comments of the discussions because those feed what
/// the whole site reads: the count behind an achievement, the snapshot a warning
/// takes of the text it names and shows in the open log, the search. A table of
/// its own is out of all of them only as long as nobody new reads it, and a read
/// is one line in a repository written by copying its neighbour - the line that
/// joins the conversation into a count or a snapshot shows on no page until a
/// stranger reads it there.
///
/// So the readers are named. The table is reached through the context set or the
/// navigation of a ticket, both spelled TicketComments, or through a generic set
/// of its entity; the files below are the ones allowed to spell any of them: the
/// model, the ticket repository and its projections, the seed, and the generators
/// of the notifications about tickets, which read the comment they announce.
///
/// The model is allowed as a whole rather than by the name of the context: it
/// declares the set and the relation and reads nothing, and whichever of its
/// files spells the relation is found by what it does
/// (<see cref="SchemaSources.ModelFiles"/>), so cutting the context into a file
/// per module moves nothing here.
/// </remarks>
public class TicketConversationReachShould
{
    /// <summary>
    /// Every way a read reaches the table: the set and the navigation by name, or
    /// a set of the entity asked for by type.
    /// </summary>
    private static readonly Regex ReachesTheTable = new(
        @"\bTicketComments\b|\bSet<\s*[\w.]*TicketComment\s*>", RegexOptions.Compiled);

    /// <summary>
    /// The files allowed to reach it, relative to src/ with forward slashes.
    /// </summary>
    private static readonly string[] Readers =
    [
        "DM.Infrastructure.Persistence/Entities/Moderation/Ticket.cs",
        "DM.Infrastructure.Persistence/Entities/Moderation/TicketComment.cs",
        "DM.Infrastructure.Persistence/Repositories/Moderation/TicketRepository.cs",
        "DM.Infrastructure.Persistence/Repositories/Moderation/ModerationMapper.cs",
        "DM.Tools.Seeder/Seeding/DataSeeder.Moderation.cs",
    ];

    /// <summary>
    /// The generators of the notifications about tickets, by the name every one of
    /// them carries.
    /// </summary>
    private static readonly Regex TicketNotifier = new(
        @"^DM\.Workers\.NotificationDispatcher/Notifiers/Moderation/Ticket\w*NotificationGenerator\.cs$",
        RegexOptions.Compiled);

    /// <summary>The generated schema files, which describe the table and read nothing.</summary>
    private const string Migrations = "DM.Infrastructure.Persistence/Migrations/";

    private static string SourceRoot => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    private static string Relative(string path) =>
        Path.GetRelativePath(SourceRoot, path).Replace('\\', '/');

    private static bool Allowed(string relative, IReadOnlySet<string> model) =>
        model.Contains(relative) ||
        Readers.Contains(relative, StringComparer.Ordinal) ||
        TicketNotifier.IsMatch(relative) ||
        relative.StartsWith(Migrations, StringComparison.Ordinal);

    [Fact]
    public void ReachTheConversationOnlyFromTheTickets()
    {
        var reaching = RepositoryFiles.ProductionSources()
            .Where(path => ReachesTheTable.IsMatch(SourceText.ReadCode(path)))
            .Select(Relative)
            .ToArray();

        reaching.Should().Contain("DM.Infrastructure.Persistence/Repositories/Moderation/TicketRepository.cs",
            "a rule that matches nothing passes: the ticket repository reads the conversation");

        var model = SchemaSources.ModelFiles.Select(Relative).ToHashSet(StringComparer.Ordinal);
        reaching.Where(relative => !Allowed(relative, model))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .Should().BeEmpty(
                "the conversation of a ticket stays out of every read of the site but the tickets' " +
                "own; a new reader is a decision, named in this file with its reason");
    }

    /// <summary>
    /// A named reader that no longer reaches the table is an open door nobody uses
    /// and the next file renamed into its place walks through.
    /// </summary>
    [Fact]
    public void KeepTheListOfReadersHonest()
    {
        foreach (var reader in Readers)
        {
            var path = Path.Combine(SourceRoot, reader);
            File.Exists(path).Should().BeTrue($"{reader} is named as a reader and does not exist");
            ReachesTheTable.IsMatch(SourceText.ReadCode(path)).Should().BeTrue(
                $"{reader} is named as a reader and reaches the conversation no more");
        }
    }
}
