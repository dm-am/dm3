using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One marker per (UserId, EntityId, EntryType) is the invariant every write
/// assumes: they all upsert on that triple. The unique index is what makes it
/// true, and the write paths are upserts so that the index refuses a duplicate
/// instead of turning an ordinary request into an error. Neither fact exists
/// anywhere but in a live store, hence the container Mongo — the application
/// host asserts the index on startup, the same way it does in a deployment.
/// </summary>
public class UnreadCountersRepositoryShould : IntegrationTestBase
{
    private static readonly DateTime MarkerRead = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public UnreadCountersRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondMarkerForTheSameKey()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var collection = Collection(scope);
        await collection.InsertOneAsync(Marker(userId, entityId));

        var act = async () => await collection.InsertOneAsync(Marker(userId, entityId));

        await act.Should().ThrowAsync<MongoWriteException>()
            .Where(e => e.WriteError.Category == ServerErrorCategory.DuplicateKey);
        var stored = await collection.CountDocumentsAsync(Key(userId, entityId));
        stored.Should().Be(1, "the store, not the caller, is what keeps the triple single");
    }

    [Fact]
    public async Task KeepOneMarkerWhenTheUserIsCountedInAgain()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        await repository.CreateAsync(entityId, UnreadEntryType.Message, new[] { userId });
        await repository.IncrementAsync(entityId, UnreadEntryType.Message);
        (await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId))[entityId]
            .Should().Be(1, "one entry went unread before the user was counted in again");

        // A participant removed from a group chat and added back: the second
        // create meets the marker the first one left.
        await repository.CreateAsync(entityId, UnreadEntryType.Message, new[] { userId });

        var stored = await Collection(scope).CountDocumentsAsync(Key(userId, entityId));
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
        await repository.CreateAsync(entityId, parentId, UnreadEntryType.Message);
        await repository.IncrementAsync(entityId, UnreadEntryType.Message);

        // Two tabs, or a double click on "mark as read".
        await repository.FlushAsync(userId, UnreadEntryType.Message, entityId);
        await repository.FlushAsync(userId, UnreadEntryType.Message, entityId);

        var stored = await Collection(scope).CountDocumentsAsync(Key(userId, entityId));
        stored.Should().Be(1);
        var unread = await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId);
        unread[entityId].Should().Be(0, "the entity was marked as read, twice");
    }

    /// <summary>
    /// A conversation is parented by the reader, not by a container it shares with
    /// the other participants. That is what makes "all my conversations" a query
    /// at all, and it is why marking one as read must not copy a parent from
    /// whichever marker the store returned first.
    /// </summary>
    /// <remarks>
    /// The stamped marker matched neither reader afterwards: not the one whose
    /// identifier it carried, because the row is keyed by user, and not its owner,
    /// because the parent was somebody else. The conversation left every
    /// parent-scoped total without appearing in any other.
    /// </remarks>
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
        await repository.CreateAsync(chatId, UnreadEntryType.Message, new[] { first, second });
        await repository.IncrementExcludingAsync(chatId, UnreadEntryType.Message, first);

        await repository.FlushAsync(second, UnreadEntryType.Message, chatId);

        var stored = await Collection(scope)
            .Find(Key(second, chatId))
            .FirstOrDefaultAsync();
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
    private static IMongoCollection<DbUnreadCounter> Collection(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>().GetCollection<DbUnreadCounter>();

    private static FilterDefinition<DbUnreadCounter> Key(Guid userId, Guid entityId) =>
        Builders<DbUnreadCounter>.Filter.Eq(c => c.UserId, userId) &
        Builders<DbUnreadCounter>.Filter.Eq(c => c.EntityId, entityId) &
        Builders<DbUnreadCounter>.Filter.Eq(c => c.EntryType, UnreadEntryType.Message);

    private static DbUnreadCounter Marker(Guid userId, Guid entityId) => new()
    {
        UserId = userId,
        EntityId = entityId,
        ParentId = entityId,
        EntryType = UnreadEntryType.Message,
        LastReadUtc = MarkerRead,
        Counter = 0
    };
}
