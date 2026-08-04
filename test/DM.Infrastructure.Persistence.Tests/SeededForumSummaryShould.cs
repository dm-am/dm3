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

        foreach (var board in SeededBoards())
        {
            board.TopicsCount.Should().Be(
                topicsPerBoard.GetValueOrDefault(board.BoardId, 0),
                "the counter of the board \"{0}\" is what a freshly migrated forum shows, and " +
                "what an anonymous visitor is shown as unread; nothing recomputes it until " +
                "somebody creates or deletes a topic there", board.Title);
        }
    }

    /// <summary>
    /// The last-topic block of a seeded board names the newest topic seeded into it.
    /// </summary>
    /// <remarks>
    /// Denormalised the same way the counter is, and left unset the same way: a board holding
    /// two topics showed an empty "last activity" column until somebody created or deleted a
    /// topic there. All five columns move together - a half-filled block is a board that names
    /// a topic without saying when it appeared.
    /// </remarks>
    [Fact]
    public void NameTheNewestTopicSeededIntoIt()
    {
        var newestPerBoard = SeededTopics()
            .GroupBy(topic => topic.BoardId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(topic => topic.CreatedUtc)
                    .ThenByDescending(topic => topic.TopicNumber)
                    .First());

        foreach (var board in SeededBoards())
        {
            var because =
                $"the last-topic block of the board \"{board.Title}\" is what a freshly " +
                "migrated forum shows as its latest activity, and nothing recomputes it until " +
                "somebody creates or deletes a topic there";

            if (!newestPerBoard.TryGetValue(board.BoardId, out var newest))
            {
                board.LastTopicId.Should().BeNull(because);
                board.LastTopicNumber.Should().BeNull(because);
                board.LastTopicTitle.Should().BeNull(because);
                board.LastTopicAuthorId.Should().BeNull(because);
                board.LastTopicCreatedUtc.Should().BeNull(because);
                continue;
            }

            board.LastTopicId.Should().Be(newest.TopicId, because);
            board.LastTopicNumber.Should().Be(newest.TopicNumber, because);
            board.LastTopicTitle.Should().Be(newest.Title, because);
            board.LastTopicAuthorId.Should().Be(newest.AuthorId, because);
            board.LastTopicCreatedUtc.Should().Be(newest.CreatedUtc, because);
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

    private sealed record SeededBoard(
        Guid BoardId,
        string Title,
        int TopicsCount,
        Guid? LastTopicId,
        int? LastTopicNumber,
        string? LastTopicTitle,
        Guid? LastTopicAuthorId,
        DateTimeOffset? LastTopicCreatedUtc);

    private sealed record SeededTopic(
        Guid TopicId,
        Guid BoardId,
        int TopicNumber,
        string Title,
        Guid AuthorId,
        DateTimeOffset CreatedUtc);

    private static IReadOnlyCollection<SeededBoard> SeededBoards() =>
        SeedRows<Board>()
            .Select(row => new SeededBoard(
                (Guid)row[nameof(Board.BoardId)]!,
                (string)row[nameof(Board.Title)]!,
                (int)row[nameof(Board.TopicsCount)]!,
                (Guid?)Optional(row, nameof(Board.LastTopicId)),
                (int?)Optional(row, nameof(Board.LastTopicNumber)),
                (string?)Optional(row, nameof(Board.LastTopicTitle)),
                (Guid?)Optional(row, nameof(Board.LastTopicAuthorId)),
                (DateTimeOffset?)Optional(row, nameof(Board.LastTopicCreatedUtc))))
            .ToList();

    private static IReadOnlyCollection<SeededTopic> SeededTopics() =>
        SeedRows<Topic>()
            .Select(row => new SeededTopic(
                (Guid)row[nameof(Topic.TopicId)]!,
                (Guid)row[nameof(Topic.BoardId)]!,
                (int)row[nameof(Topic.TopicNumber)]!,
                (string)row[nameof(Topic.Title)]!,
                (Guid)row[nameof(Topic.AuthorId)]!,
                (DateTimeOffset)row[nameof(Topic.CreatedUtc)]!))
            .ToList();

    private static IReadOnlyDictionary<Guid, int> SeededTopicsPerBoard() =>
        SeededTopics()
            .GroupBy(topic => topic.BoardId)
            .ToDictionary(group => group.Key, group => group.Count());

    /// <summary>
    /// A column the seed may leave unset: HasData over entity instances carries every property,
    /// HasData over anonymous objects carries only the named ones.
    /// </summary>
    private static object? Optional(IDictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) ? value : null;

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
