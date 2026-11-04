using System.Net;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Board = DM.Infrastructure.Persistence.Entities.Forum.Board;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Reading one board fills two unread counters for a signed-in reader, from the one DbContext of the request
/// scope, which serves one operation at a time.
/// </summary>
/// <remarks>
/// The overlap itself is BoardServiceShould.ReadTheCountersOfOneBoardOneAfterTheOther's; the counters are
/// asserted because a status alone does not tell which branch answered.
/// </remarks>
public class BoardUnreadCountersShould : IntegrationTestBase
{
    public BoardUnreadCountersShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CountNothingUnreadForAReaderWithNoUnreadEntries()
    {
        var (boardId, alias) = await CreateBoardWithTopic();
        try
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/boards/{alias}"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var (topicsCount, unreadTopicsCount) = await ReadCounters(response);
            topicsCount.Should().Be(1);
            // The signed-in branch is the one that reads the counters: this
            // reader has no unread entry, so its answer differs from the total.
            unreadTopicsCount.Should().Be(0);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task ShowEveryTopicAsUnreadToAGuest()
    {
        var (boardId, alias) = await CreateBoardWithTopic();
        try
        {
            var response = await Client.GetAsync($"/v1/boards/{alias}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var (topicsCount, unreadTopicsCount) = await ReadCounters(response);
            unreadTopicsCount.Should().Be(topicsCount).And.Be(1);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    private static async Task<(int TopicsCount, int UnreadTopicsCount)> ReadCounters(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resource = document.RootElement.GetProperty("resource");
        return (resource.GetProperty("topicsCount").GetInt32(),
            resource.GetProperty("unreadTopicsCount").GetInt32());
    }

    private async Task<(Guid Id, string Alias)> CreateBoardWithTopic()
    {
        var boardId = Guid.NewGuid();
        var alias = $"unread-{boardId:N}";
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Boards.Add(new Board
        {
            BoardId = boardId,
            Title = $"Unread board {boardId:N}",
            Alias = alias,
            Order = 0,
            CreateTopicPolicy = BoardAccessPolicy.Guest,
        });
        await dbContext.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        await repository.Create(
            new CreateTopicEntity { TopicId = Guid.NewGuid(), Title = "Unread topic", Text = "text" },
            TestConstants.TestUserId, boardId);

        return (boardId, alias);
    }

    private async Task DropBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Boards
            .Where(b => b.BoardId == boardId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastTopicId, (Guid?)null));
        await dbContext.Topics.IgnoreQueryFilters().Where(t => t.BoardId == boardId).ExecuteDeleteAsync();
        await dbContext.Boards.Where(b => b.BoardId == boardId).ExecuteDeleteAsync();
    }
}
