using System;
using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

// The topic counter is denormalised and a migrated database holds what the seed said.
public class SeededForumSummaryShould
{
    [Fact]
    public void CountTheTopicsThatAreSeededIntoIt()
    {
        var topicsPerBoard = SeededTopicsPerBoard();
        var boards = SeededBoards();
        boards.Should().NotBeEmpty("no board read means the seed is read from the runtime model, which drops HasData");
        topicsPerBoard.Values.Sum().Should().BePositive(
            "against no seeded topic the rule holds only while every counter is zero");

        foreach (var board in boards)
        {
            board.TopicsCount.Should().Be(
                topicsPerBoard.GetValueOrDefault(board.BoardId, 0),
                "the counter of the board \"{0}\" is what a freshly migrated forum shows, and " +
                "what an anonymous visitor is shown as unread; nothing recomputes it until " +
                "somebody creates or deletes a topic there", board.Title);
        }
    }

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

        var boards = SeededBoards();
        boards.Should().NotBeEmpty("no board read means the seed is read from the runtime model, which drops HasData");

        foreach (var board in boards)
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
        ProbeContext.SeedRows<Board>()
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
        ProbeContext.SeedRows<Topic>()
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

    // A column the seed may leave unset: HasData over entity instances carries every property,
    // HasData over anonymous objects carries only the named ones.
    private static object? Optional(IDictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) ? value : null;
}
