using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Two collections used to grow without a ceiling: notifications, which nothing
/// ever deleted, and the tombstones an unread marker left behind on every deleted
/// topic, room and conversation. Neither is a failure anyone would notice, which
/// is why the limit has to be asserted rather than remembered — an expiry exists
/// only as an index descriptor on the server, and the host is what puts it there.
/// </summary>
public class MongoRetentionShould : IntegrationTestBase
{
    public MongoRetentionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ExpireNotifications()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        var expiry = await IndexOf<DbNotification>(scope, "IX_RealtimeNotifications_Expiry");

        expiry.Should().NotBeNull("nothing in the code deletes a notification");
        expiry!["expireAfterSeconds"].ToInt64().Should().BePositive();
        expiry["key"].AsBsonDocument.Names.Should().Contain(nameof(DbNotification.CreatedUtc));
    }

    [Fact]
    public async Task ExpireTombstonedUnreadMarkers()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        var expiry = await IndexOf<DbUnreadCounter>(scope, "IX_UnreadCounters_Expiry");

        expiry.Should().NotBeNull();
        expiry!["expireAfterSeconds"].ToInt64().Should().BePositive();
        expiry["key"].AsBsonDocument.Names.Should().Contain(nameof(DbUnreadCounter.RemovedUtc));
    }

    /// <summary>
    /// The expiry reads a moment, so removing a marker has to write one. A flag
    /// alone leaves the TTL index nothing to look at and the document forever.
    /// </summary>
    [Fact]
    public async Task StampTheMomentAMarkerIsRemoved()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        await repository.CreateMarkerAsync(entityId, UnreadEntryType.Message, new[] { userId });

        await repository.DeleteAsync(entityId, UnreadEntryType.Message);

        var stored = await Collection<DbUnreadCounter>(scope)
            .Find(Builders<DbUnreadCounter>.Filter.Eq(c => c.EntityId, entityId))
            .FirstAsync();
        stored.IsRemoved.Should().BeTrue();
        stored.RemovedUtc.Should().NotBeNull("a tombstone with no moment never expires");
    }

    private static IMongoCollection<TEntity> Collection<TEntity>(IServiceScope scope)
        where TEntity : class =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>().GetCollection<TEntity>();

    private static async Task<BsonDocument?> IndexOf<TEntity>(IServiceScope scope, string name)
        where TEntity : class
    {
        var indexes = await (await Collection<TEntity>(scope).Indexes.ListAsync()).ToListAsync();
        return indexes.SingleOrDefault(i => i["name"].AsString == name);
    }
}
