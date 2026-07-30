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
        await repository.CreateAsync(entityId, UnreadEntryType.Message, new[] {userId});
        await repository.IncrementAsync(entityId, UnreadEntryType.Message);
        (await repository.SelectByEntitiesAsync(userId, UnreadEntryType.Message, entityId))[entityId]
            .Should().Be(1, "one entry went unread before the user was counted in again");

        // A participant removed from a group chat and added back: the second
        // create meets the marker the first one left.
        await repository.CreateAsync(entityId, UnreadEntryType.Message, new[] {userId});

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
