using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Topic = DM.Domain.Forum.Features.Topics.Topic;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// A board carries a denormalized topic count and a "last topic" block. They used
/// to be raised on creation and adjusted nowhere else, so deleting a topic or
/// moving one between boards left the board claiming a count it did not have and
/// pointing at a topic that was no longer there — with no path back short of
/// editing the database by hand.
/// </summary>
public class BoardTopicSummaryShould : IntegrationTestBase
{
    /// <summary>
    /// How long the queued call is watched before it is called blocked. Long
    /// enough that a call which does not wait at all has finished; short enough
    /// that it is paid once per test.
    /// </summary>
    private static readonly TimeSpan Probe = TimeSpan.FromSeconds(2);

    /// <summary>The bound on the whole wait, so a lock nobody releases fails.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many topics share one timestamp in the tie-break case. More than two,
    /// so that a reading without the tie-break has to guess rather than choose
    /// between a pair.
    /// </summary>
    private const int TiedTopics = 5;

    public BoardTopicSummaryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CountTheTopicsCreatedInTheBoard()
    {
        var boardId = await CreateBoard();
        try
        {
            await CreateTopic(boardId, 0);
            var second = await CreateTopic(boardId, 1);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(2);
            board.LastTopicId.Should().Be(second.Id);
            board.LastTopicNumber.Should().Be(second.TopicNumber);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task DropADeletedTopicFromTheCountAndFallBackToThePreviousOne()
    {
        var boardId = await CreateBoard();
        try
        {
            var first = await CreateTopic(boardId, 0);
            var second = await CreateTopic(boardId, 1);

            await DeleteTopic(second.Id);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(1);
            board.LastTopicId.Should().Be(first.Id);
            board.LastTopicTitle.Should().Be(first.Title);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task ClearTheSummaryWhenTheLastTopicIsDeleted()
    {
        var boardId = await CreateBoard();
        try
        {
            var only = await CreateTopic(boardId, 0);

            await DeleteTopic(only.Id);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(0);
            board.LastTopicId.Should().BeNull();
            board.LastTopicNumber.Should().BeNull();
            board.LastTopicTitle.Should().BeNull();
            board.LastTopicAuthorId.Should().BeNull();
            board.LastTopicCreatedUtc.Should().BeNull();
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task MoveTheSummaryWithTheTopicWhenItChangesBoard()
    {
        var source = await CreateBoard();
        var destination = await CreateBoard();
        try
        {
            var moved = await CreateTopic(source, 0);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
                await repository.Update(new UpdateTopicEntity { TopicId = moved.Id }, destination);
            }

            var sourceBoard = await ReadBoard(source);
            var destinationBoard = await ReadBoard(destination);

            sourceBoard.TopicsCount.Should().Be(0);
            sourceBoard.LastTopicId.Should().BeNull();

            destinationBoard.TopicsCount.Should().Be(1);
            destinationBoard.LastTopicId.Should().Be(moved.Id);
        }
        finally
        {
            await DropBoard(source);
            await DropBoard(destination);
        }
    }

    /// <summary>
    /// A topic created while another is being deleted is in the count the delete
    /// leaves behind.
    /// </summary>
    /// <remarks>
    /// The summary is read from the topics and written back to the board, so two
    /// of those running at once used to end with the later one writing the number
    /// it had read before the earlier one committed - a board short one topic, and
    /// short it stays until somebody writes the board again. The create is held
    /// open by hand, in the shape the repository creates one in (the board row
    /// taken first, then the topic, then the summary), because the ordering that
    /// broke the count is a create the delete's read cannot see and that commits
    /// before the delete's own write; only a live server can be asked whether one
    /// write waits for another, which is why this is here and not in the domain
    /// tests. The delete is the real call, so what is under test is the order of
    /// its lock against its read.
    /// </remarks>
    [Fact]
    public async Task CountTheTopicsThatSurviveWhenACreateAndADeleteMeetOnOneBoard()
    {
        var boardId = await CreateBoard();
        try
        {
            var deleted = await CreateTopic(boardId, 0);
            var createdConcurrently = Guid.NewGuid();
            var createdUtc = DateTimeOffset.UtcNow;

            await using var creating = DatabaseFixture.CreateDbContext();
            await using var create = await creating.Database.BeginTransactionAsync();
            await creating.Database.ExecuteSqlRawAsync(
                """SELECT "BoardId" FROM "Boards" WHERE "BoardId" = {0} FOR UPDATE""", boardId);
            await creating.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Topics" ("TopicId", "BoardId", "TopicNumber", "AuthorId", "Title", "Text",
                                      "CreatedUtc", "IsRemoved", "IsClosed", "ClosedByAuthor", "IsAttached")
                VALUES ({0}, {1}, 2, {2}, 'Concurrent topic', '', {3}, false, false, false, false)
                """,
                createdConcurrently, boardId, TestConstants.TestUserId, createdUtc);
            await creating.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Boards"
                SET "TopicsCount" = 2, "LastTopicId" = {0}, "LastTopicNumber" = 2,
                    "LastTopicTitle" = 'Concurrent topic', "LastTopicAuthorId" = {1},
                    "LastTopicCreatedUtc" = {2}
                WHERE "BoardId" = {3}
                """,
                createdConcurrently, TestConstants.TestUserId, createdUtc, boardId);

            var delete = Task.Run(() => DeleteTopic(deleted.Id));
            var decidedWhileTheCreateWasOpen = await Task.WhenAny(delete, Task.Delay(Probe)) == delete;

            await create.CommitAsync();
            await delete.WaitAsync(Bound);

            decidedWhileTheCreateWasOpen.Should().BeFalse(
                "the delete takes the board row before it counts the topics it will write back, " +
                "and the create in flight holds that row");

            var board = await ReadBoard(boardId);
            board.TopicsCount.Should().Be(1,
                "one topic went and one arrived, so the board is left with the one that arrived");
            board.LastTopicId.Should().Be(createdConcurrently,
                "the recompute runs after the create it waited for, so it sees that topic");
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    /// <summary>
    /// A recompute asked for from outside empties the block when the board has
    /// nothing left to name.
    /// </summary>
    /// <remarks>
    /// The digest used to carry a second reading of "the last topic" - it took the
    /// freshest topic and wrote the five columns from it, with no board lock and
    /// no fallback for a board holding none. On a board whose topics had all gone
    /// it threw, and the recompute the topic paths run answers the same question
    /// without either fault. The topic here is marked removed behind the
    /// repository's back, which is what leaves the board naming a row every read
    /// filters out.
    /// </remarks>
    [Fact]
    public async Task EmptyTheSummaryOfABoardWhoseTopicsAreAllGone()
    {
        var boardId = await CreateBoard();
        try
        {
            var only = await CreateTopic(boardId, 0);
            await MarkRemoved(only.Id);

            await RefreshSummary(boardId);

            var board = await ReadBoard(boardId);
            board.TopicsCount.Should().Be(0);
            board.LastTopicId.Should().BeNull(
                "a board with nothing to name names nothing, and asking it to is the one " +
                "case the second reading of this could not answer at all");
            board.LastTopicNumber.Should().BeNull();
            board.LastTopicTitle.Should().BeNull();
            board.LastTopicAuthorId.Should().BeNull();
            board.LastTopicCreatedUtc.Should().BeNull();
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    /// <summary>
    /// The recompute asked for from outside reads "the last topic" the way the
    /// topic paths do, tie-break included.
    /// </summary>
    /// <remarks>
    /// Two topics can share a timestamp - the digest backdates one to midnight of
    /// the first of a month, and a seeded board carries whole rows stamped
    /// together. Ordered by the timestamp alone, which topic is the board's last
    /// one is whichever the plan happened to return, and the answer differed
    /// between the two writers of these columns: the board flipped between them
    /// as the two took turns writing it.
    /// </remarks>
    [Fact]
    public async Task NameTheSameLastTopicTheTopicPathsWouldOnATie()
    {
        var boardId = await CreateBoard();
        try
        {
            // Whole microseconds: the column keeps that much and no more, so a
            // moment carrying finer ticks would be compared against a value the
            // database had rounded.
            var sharedMoment = Microseconds(DateTimeOffset.UtcNow);
            var topics = new List<Topic>();
            for (var index = 0; index < TiedTopics; index++)
            {
                topics.Add(await CreateTopic(boardId, index));
            }

            await StampAtOneMoment(boardId, sharedMoment);
            await RefreshSummary(boardId);

            var board = await ReadBoard(boardId);
            var highest = topics.OrderByDescending(t => t.TopicNumber).First();

            board.TopicsCount.Should().Be(TiedTopics);
            board.LastTopicNumber.Should().Be(highest.TopicNumber,
                "the number is the tie-break both writers of this block have to share, and " +
                "the highest of the tied topics is the one the board came to last");
            board.LastTopicId.Should().Be(highest.Id);
            board.LastTopicTitle.Should().Be(highest.Title);
            board.LastTopicCreatedUtc.Should().Be(sharedMoment);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    /// <summary>
    /// A topic moved into a board that already numbers a topic the way it is
    /// numbered takes the next free number there.
    /// </summary>
    /// <remarks>
    /// The number is per board and the topic used to keep the one it had, so a
    /// move into a board whose numbering had reached that far was refused
    /// outright by the unique index on (BoardId, TopicNumber): the moderator saw
    /// a 500 and the topic stayed where it was, and which pairs of boards this
    /// happened between was a matter of how many topics each had.
    ///
    /// What the move costs is the old address: /forum/:alias/:num of the board it
    /// left stops resolving, which is what a number meaning "the Nth topic of
    /// this board" is. Every link built on the identifier still opens it.
    /// </remarks>
    [Fact]
    public async Task GiveAMovedTopicTheNextFreeNumberOfTheBoardItJoins()
    {
        var source = await CreateBoard();
        var destination = await CreateBoard();
        try
        {
            // The topic that stays put is created first, so the one that moves is
            // the newer of the two and the destination's summary has to end up on
            // it: a move that renumbered the topic and left the block behind would
            // otherwise pass unnoticed.
            var sitting = await CreateTopic(destination, 0);
            var moved = await CreateTopic(source, 1);

            moved.TopicNumber.Should().Be(sitting.TopicNumber,
                "both boards are new, so the two topics are numbered alike - which is the " +
                "collision this test is about");

            Topic afterMove;
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
                var result = await repository.Update(
                    new UpdateTopicEntity { TopicId = moved.Id }, destination);
                afterMove = result.Topic;
            }

            afterMove.TopicNumber.Should().Be(sitting.TopicNumber + 1,
                "the answer of the move carries the number the topic now lives under, which " +
                "is what the client builds the address from");

            var sourceBoard = await ReadBoard(source);
            var destinationBoard = await ReadBoard(destination);

            sourceBoard.TopicsCount.Should().Be(0);
            sourceBoard.LastTopicId.Should().BeNull();

            destinationBoard.TopicsCount.Should().Be(2);
            destinationBoard.LastTopicId.Should().Be(moved.Id);
            destinationBoard.LastTopicNumber.Should().Be(afterMove.TopicNumber);
        }
        finally
        {
            await DropBoard(source);
            await DropBoard(destination);
        }
    }

    /// <summary>The same moment at the resolution the column stores.</summary>
    private static DateTimeOffset Microseconds(DateTimeOffset moment) =>
        new(moment.Ticks - moment.Ticks % TimeSpan.TicksPerMicrosecond, moment.Offset);

    private async Task RefreshSummary(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        await repository.RefreshBoardSummary(boardId);
    }

    /// <summary>
    /// Soft-delete straight to the row: the point is a board left naming a topic
    /// no read returns, which the repository's own deletion would have repaired
    /// on its way out.
    /// </summary>
    private async Task MarkRemoved(Guid topicId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Topics
            .Where(t => t.TopicId == topicId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));
    }

    /// <summary>Every topic of the board stamped with one and the same moment.</summary>
    private async Task StampAtOneMoment(Guid boardId, DateTimeOffset moment)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Topics
            .Where(t => t.BoardId == boardId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedUtc, moment));
    }

    /// <summary>
    /// The comment counter and the last-comment block are computed on read, so a
    /// new comment has to show up on the very next render.
    /// </summary>
    /// <remarks>
    /// They used to be columns that only the demo seeder ever wrote: in production
    /// the forum showed zero comments on every board forever, and "last activity"
    /// could never name a comment.
    /// </remarks>
    [Fact]
    public async Task CountCommentsOfTheBoardAsSoonAsTheyAppear()
    {
        var boardId = await CreateBoard();
        try
        {
            var topic = await CreateTopic(boardId, 0);

            var before = await ReadBoardSummary(boardId);
            before.CommentsCount.Should().Be(0);
            before.LastComment.Should().BeNull();

            await AddComment(topic.Id, "First");
            await AddComment(topic.Id, "Second");

            var after = await ReadBoardSummary(boardId);
            after.CommentsCount.Should().Be(2);
            after.LastComment.Should().NotBeNull();
            after.LastComment!.TopicId.Should().Be(topic.Id);
        }
        finally
        {
            await PostTestHelper.DropComments(DatabaseFixture, boardId);
            await DropBoard(boardId);
        }
    }

    private async Task AddComment(Guid topicId, string text)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Comment>().Add(new()
        {
            CommentId = Guid.NewGuid(),
            EntityId = topicId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = text,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<DM.Domain.Forum.Features.Boards.Board> ReadBoardSummary(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<DM.Domain.Forum.Features.Boards.IBoardRepository>();
        var boards = await repository.SelectBoards(null);
        return boards.First(b => b.Id == boardId);
    }

    private async Task<Topic> CreateTopic(Guid boardId, int index)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        return await repository.Create(
            new CreateTopicEntity { TopicId = Guid.NewGuid(), Title = $"Summary topic {index}", Text = "text" },
            TestConstants.TestUserId, boardId);
    }

    private async Task DeleteTopic(Guid topicId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        await repository.Delete(topicId, TestConstants.TestUserId);
    }

    private async Task<Board> ReadBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        return await dbContext.Boards.AsNoTracking().FirstAsync(b => b.BoardId == boardId);
    }

    private async Task<Guid> CreateBoard()
    {
        var boardId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Boards.Add(new Board
        {
            BoardId = boardId,
            Title = $"Summary board {boardId:N}",
            Alias = $"summary-{boardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.Guest,
        });
        await dbContext.SaveChangesAsync();
        return boardId;
    }

    private async Task DropBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        // The board points back at its last topic, so the denormalized link has to
        // be released before the topics it references can go.
        await dbContext.Boards
            .Where(b => b.BoardId == boardId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastTopicId, (Guid?)null));
        await dbContext.Topics.IgnoreQueryFilters().Where(t => t.BoardId == boardId).ExecuteDeleteAsync();
        await dbContext.Boards.Where(b => b.BoardId == boardId).ExecuteDeleteAsync();
    }
}
