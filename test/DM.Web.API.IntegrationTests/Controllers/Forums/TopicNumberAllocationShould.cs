using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Forum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Topic = DM.Domain.Forum.Features.Topics.Topic;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// TopicNumber is the canonical topic URL key and it is allocated as MAX+1 with
/// no lock, so two creates in the same board can read the same maximum. Before
/// the unique index both inserts committed and one of the two topics became
/// permanently unreachable by its own link, with nothing to even detect it.
/// </summary>
public class TopicNumberAllocationShould : IntegrationTestBase
{
    private const int Parallelism = 8;

    public TopicNumberAllocationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GiveConcurrentTopicsOfOneBoardDistinctNumbers()
    {
        var boardId = await CreateBoard();
        try
        {
            var topics = await Task.WhenAll(Enumerable
                .Range(0, Parallelism)
                .Select(i => CreateTopic(boardId, i)));

            topics.Select(t => t.TopicNumber).Should().BeEquivalentTo(Enumerable.Range(1, Parallelism));
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task RejectADuplicateNumberInTheSameBoard()
    {
        var boardId = await CreateBoard();
        try
        {
            await CreateTopic(boardId, 0);

            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var insertDuplicate = async () => await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Topics" ("TopicId", "BoardId", "TopicNumber", "AuthorId", "Title", "Text",
                                      "CreatedUtc", "IsRemoved", "IsClosed", "IsAttached")
                VALUES ({0}, {1}, 1, {2}, 'duplicate', '', now(), false, false, false)
                """,
                Guid.NewGuid(), boardId, TestConstants.TestUserId);

            (await insertDuplicate.Should().ThrowAsync<PostgresException>())
                .Which.ConstraintName.Should().Be("IX_Topics_BoardId_TopicNumber");
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    /// <summary>
    /// A removed topic keeps its number: its link has to stay a 410 rather than
    /// start resolving to whatever was created after it.
    /// </summary>
    [Fact]
    public async Task NotHandOutTheNumberOfARemovedTopic()
    {
        var boardId = await CreateBoard();
        try
        {
            var removed = await CreateTopic(boardId, 0);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
                await dbContext.Topics
                    .Where(t => t.TopicId == removed.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));
            }

            var next = await CreateTopic(boardId, 1);

            next.TopicNumber.Should().Be(removed.TopicNumber + 1);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    private async Task<Topic> CreateTopic(Guid boardId, int index)
    {
        // One scope per concurrent create: a DbContext is scoped and must not be
        // shared between parallel calls.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        return await repository.Create(
            new CreateTopicEntity { TopicId = Guid.NewGuid(), Title = $"Concurrent topic {index}", Text = "text" },
            TestConstants.TestUserId, boardId);
    }

    private async Task<Guid> CreateBoard()
    {
        var boardId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Boards.Add(new Board
        {
            BoardId = boardId,
            Title = $"Allocation board {boardId:N}",
            Alias = $"allocation-{boardId:N}",
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
        // be released before the topics it references can go. The last-comment link
        // is gone: that value is computed on read now.
        await dbContext.Database.ExecuteSqlRawAsync(
            """UPDATE "Boards" SET "LastTopicId" = NULL WHERE "BoardId" = {0}""",
            boardId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Topics" WHERE "BoardId" = {0}""", boardId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Boards" WHERE "BoardId" = {0}""", boardId);
    }
}
