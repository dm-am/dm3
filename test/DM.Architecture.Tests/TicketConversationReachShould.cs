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
/// Ticket comments have a table of their own, out of the counts, snapshots and search the whole site
/// reads, so the readers are named. The model is allowed as a whole, found by
/// <see cref="SchemaSources.ModelFiles"/>.
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
        "DM.Tools.Seeder/Seeding/DataSeeder.Tickets.cs",
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
    /// A named reader that no longer reaches the table is an open door.
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
