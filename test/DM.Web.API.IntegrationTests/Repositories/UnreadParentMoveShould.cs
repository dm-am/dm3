using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A topic moved to another board takes its own unread markers and no others.
/// </summary>
/// <remarks>
/// A marker names the entity it belongs to and the container it is counted
/// under. Moving one used to select by the container, so moving a single topic
/// re-parented every marker of the board it left: the board a reader was on
/// stopped counting anything, and the board it moved to started counting topics
/// that were never there. Nothing said so - the write answered without an error
/// and the numbers were simply wrong from then on.
///
/// Run against the container Postgres because the write is a set-based
/// ExecuteUpdate: the statement it produces is what decides which rows move.
/// </remarks>
public class UnreadParentMoveShould : IntegrationTestBase
{
    public UnreadParentMoveShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly DateTime Read = new(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MoveTheMarkersOfTheNamedEntityAndLeaveItsNeighboursAlone()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var oldBoardId = Guid.NewGuid();
        var newBoardId = Guid.NewGuid();
        var movedTopicId = Guid.NewGuid();
        var stayingTopicId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        dbContext.UnreadCounters.AddRange(
            Marker(readerId, movedTopicId, oldBoardId),
            Marker(readerId, stayingTopicId, oldBoardId));
        await dbContext.SaveChangesAsync();

        await repository.ChangeParentAsync(movedTopicId, UnreadEntryType.Message, newBoardId);

        var markers = await dbContext.UnreadCounters
            .AsNoTracking()
            .Where(c => c.UserId == readerId)
            .ToListAsync();

        markers.Single(c => c.EntityId == movedTopicId).ParentId.Should().Be(newBoardId);
        markers.Single(c => c.EntityId == stayingTopicId).ParentId.Should().Be(oldBoardId);
    }

    private static DbUnreadCounter Marker(Guid userId, Guid entityId, Guid parentId) => new()
    {
        UserId = userId,
        EntityId = entityId,
        EntryType = UnreadEntryType.Message,
        ParentId = parentId,
        LastReadUtc = Read,
        Counter = 1
    };
}
