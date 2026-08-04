using System;
using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Forum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// A seeded board agrees with the topics seeded into it.
/// </summary>
/// <remarks>
/// The topic counter of a board is denormalised: only a topic write recomputes it, so on a
/// database that has just been migrated the column says whatever the seed said. It said one for
/// a board holding two topics, and the forum showed one - and one unread besides, because the
/// same number is what a visitor who has never opened the board is shown as unread.
///
/// Read out of the design-time model rather than restated here, so the rule follows the seed
/// instead of a copy of it. The board summary tests next door each create their own board and
/// therefore never look at the seeded one, which is why the drift survived them.
/// </remarks>
public class SeededForumSummaryShould
{
    [Fact]
    public void CountTheTopicsThatAreSeededIntoIt()
    {
        var topicsPerBoard = SeededTopicsPerBoard();

        foreach (var (boardId, title, topicsCount) in SeededBoards())
        {
            topicsCount.Should().Be(
                topicsPerBoard.GetValueOrDefault(boardId, 0),
                "the counter of the board \"{0}\" is what a freshly migrated forum shows, and " +
                "what an anonymous visitor is shown as unread; nothing recomputes it until " +
                "somebody creates or deletes a topic there", title);
        }
    }

    /// <summary>
    /// A reader that reads nothing passes everything.
    /// </summary>
    [Fact]
    public void ReadTheSeededRows()
    {
        SeededBoards().Should().HaveCountGreaterThan(5,
            "the forum is seeded with a full set of boards; finding one or none means the " +
            "seed is being read from the runtime model, which drops HasData");
        SeededTopicsPerBoard().Values.Sum().Should().BeGreaterThan(0,
            "the rule compares against the seeded topics, and against none of them it holds " +
            "only while every counter is zero");
    }

    private static IReadOnlyCollection<(Guid BoardId, string Title, int TopicsCount)> SeededBoards() =>
        SeedRows<Board>()
            .Select(row => (
                BoardId: (Guid)row[nameof(Board.BoardId)]!,
                Title: (string)row[nameof(Board.Title)]!,
                TopicsCount: (int)row[nameof(Board.TopicsCount)]!))
            .ToList();

    private static IReadOnlyDictionary<Guid, int> SeededTopicsPerBoard() =>
        SeedRows<Topic>()
            .GroupBy(row => (Guid)row[nameof(Topic.BoardId)]!)
            .ToDictionary(group => group.Key, group => group.Count());

    private static IReadOnlyCollection<IDictionary<string, object?>> SeedRows<TEntity>()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=seed-forum-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);

        // HasData lives in the design-time model; the runtime one drops it.
        var model = context.GetService<IDesignTimeModel>().Model;

        return model.FindEntityType(typeof(TEntity))!.GetSeedData().ToList();
    }
}
