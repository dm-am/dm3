using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The unique index on (EventId, EventType) turns a second handling of one outbox row into one notification.
/// </summary>
public class NotificationIndexShould : IntegrationTestBase
{
    public NotificationIndexShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task SwallowASecondNotificationOfOneEvent()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var eventId = Guid.NewGuid();

        var first = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        first.Notifications.Add(Notification(eventId));
        await first.SaveChangesAsync();

        using var second = DatabaseFixture.CreateDbContext();
        second.Notifications.Add(Notification(eventId));
        var duplicate = async () => await second.SaveChangesAsync();

        (await duplicate.Should().ThrowAsync<DbUpdateException>(
                "one event fans out to at most one notification per output type - the " +
                "invariant a row handled twice leans on"))
            .WithInnerException<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505");
    }

    private static DbNotification Notification(Guid eventId) => new()
    {
        NotificationId = Guid.NewGuid(),
        EventId = eventId,
        EventType = EventType.NewGame,
        CreatedUtc = DateTimeOffset.UtcNow,
        Metadata = "{}",
    };
}
