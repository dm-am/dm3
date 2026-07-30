using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Nothing in the store keeps one marker per (UserId, EntityId, EntryType): the
/// writes upsert on that triple and no unique index backs it, so two flushes
/// racing each other leave two documents. The aggregate reads survive that by
/// grouping; these two read the documents directly and have to stay defined.
/// Runs against the container Mongo because the duplicate exists nowhere else.
/// </summary>
public class UnreadCountersRepositoryShould : IntegrationTestBase
{
    private static readonly DateTime EarlierRead = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterRead = new(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

    public UnreadCountersRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReportTheLatestOfTwoMarkersLeftForOneEntity()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await WriteTwoMarkers(scope, userId, entityId);

        var lastRead = await scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>()
            .GetLastReadTimeAsync(userId, entityId, UnreadEntryType.Message);

        lastRead.Should().Be(LaterRead,
            "the later marker is the read the user actually made last");
    }

    [Fact]
    public async Task AnswerForAnEntityThatCarriesTwoMarkers()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await WriteTwoMarkers(scope, userId, entityId);

        var lastReadTimes = await scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>()
            .GetLastReadTimesAsync(userId, UnreadEntryType.Message, entityId);

        lastReadTimes.Should().ContainKey(entityId,
            "the caller is the jump to the first unread post, and it fails whole on an exception");
        lastReadTimes[entityId].Should().Be(LaterRead);
    }

    /// <summary>
    /// Written straight to the collection: the pair takes two upserts racing each
    /// other, which no test can arrange on demand, while the store permits the
    /// result. The earlier marker goes in first, so natural order answers wrong.
    /// </summary>
    private static Task WriteTwoMarkers(IServiceScope scope, Guid userId, Guid entityId) =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>()
            .GetCollection<DbUnreadCounter>()
            .InsertManyAsync(new[]
            {
                Marker(userId, entityId, EarlierRead),
                Marker(userId, entityId, LaterRead)
            });

    private static DbUnreadCounter Marker(Guid userId, Guid entityId, DateTime lastReadUtc) => new()
    {
        UserId = userId,
        EntityId = entityId,
        ParentId = entityId,
        EntryType = UnreadEntryType.Message,
        LastReadUtc = lastReadUtc,
        Counter = 0
    };
}
