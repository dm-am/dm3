using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

// One marker per (UserId, EntityId, EntryType): the primary key holds it and the writes are INSERT
// ... ON CONFLICT, both of which exist only in a live store.
public class UnreadCountersRepositoryShould : IntegrationTestBase
{
    public UnreadCountersRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task KeepOneMarkerWhenTheUserIsCountedInAgain()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        await repository.CreateMarkerAsync(entityId, UnreadEntryType.Message, new[] { userId });
        await repository.IncrementAsync(entityId, UnreadEntryType.Message);
        (await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId))[entityId]
            .Should().Be(1, "one entry went unread before the user was counted in again");

        // A participant removed from a group chat and added back: the second
        // create meets the marker the first one left.
        await repository.CreateMarkerAsync(entityId, UnreadEntryType.Message, new[] { userId });

        var stored = await Count(scope, userId, entityId);
        stored.Should().Be(1);
        var unread = await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId);
        unread[entityId].Should().Be(0, "the count starts over for a user counted in again");
    }

    [Fact]
    public async Task KeepOneMarkerWhenTheSameEntityIsFlushedTwice()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        await repository.CreateMarkerAsync(entityId, parentId, UnreadEntryType.Message);
        await repository.IncrementAsync(entityId, UnreadEntryType.Message);

        // Two tabs, or a double click on "mark as read".
        await repository.FlushAsync(userId, UnreadEntryType.Message, entityId);
        await repository.FlushAsync(userId, UnreadEntryType.Message, entityId);

        var stored = await Count(scope, userId, entityId);
        stored.Should().Be(1);
        var unread = await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId);
        unread[entityId].Should().Be(0, "the entity was marked as read, twice");
    }

    // A conversation is parented by the reader, so marking it read does not copy a parent from
    // another marker.
    [Fact]
    public async Task KeepTheReadersOwnParentWhenAnotherParticipantsMarkerComesFirst()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();

        // Both markers are parented by their own owner, the way a conversation
        // creates them.
        await repository.CreateMarkerAsync(chatId, UnreadEntryType.Message, new[] { first, second });
        await repository.IncrementExcludingAsync(chatId, UnreadEntryType.Message, first);

        await repository.FlushAsync(second, UnreadEntryType.Message, chatId);

        var stored = await Context(scope).UnreadCounters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == second && c.EntityId == chatId &&
                                      c.EntryType == UnreadEntryType.Message);
        stored.Should().NotBeNull();
        stored!.ParentId.Should().Be(second,
            "the reader owns the parent of their own marker, and marking as read does not move it");
        stored.Counter.Should().Be(0, "marking as read is still what this does");

        // The marker has to remain reachable by its parent, which is the whole
        // point of the field: a new message after the flush must show up in the
        // reader own total. A marker stamped with somebody else parent
        // matches neither them nor its owner, and the conversation goes quiet.
        await repository.IncrementExcludingAsync(chatId, UnreadEntryType.Message, first);
        var mine = await repository.SelectByParentsAsync(second, UnreadEntryType.Message, second);
        mine.Should().ContainKey(second);
        mine[second].Should().Be(1,
            "a conversation that lost its parent disappears from every total that asks by parent");
    }

    // The retention sweep reads a moment, so removing a marker writes one.
    [Fact]
    public async Task StampTheMomentAMarkerIsRemoved()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        await repository.CreateMarkerAsync(entityId, UnreadEntryType.Message, new[] { userId });

        await repository.DeleteAsync(entityId, UnreadEntryType.Message);

        var stored = await Context(scope).UnreadCounters
            .AsNoTracking()
            .FirstAsync(c => c.EntityId == entityId);
        stored.IsRemoved.Should().BeTrue();
        stored.RemovedUtc.Should().NotBeNull("a tombstone with no moment never expires");
    }

    private static DmDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>();

    private static Task<int> Count(IServiceScope scope, Guid userId, Guid entityId) =>
        Context(scope).UnreadCounters
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.EntityId == entityId &&
                             c.EntryType == UnreadEntryType.Message);
}
