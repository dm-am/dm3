using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Retention;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DbSecurityAuditEntry = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using DM.Infrastructure.Persistence.Entities.Shared;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The retention sweep is one DELETE per policy by the stamped column: an older row goes, a younger one stays.
/// Against a live Postgres: the sweep is raw SQL over the registry.
/// </summary>
public class RetentionSweepShould : IntegrationTestBase
{
    public RetentionSweepShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ExpireLoginAttemptsPastTheLockoutWindow()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = $"expired-{Guid.NewGuid():N}@t|1.2.3.4";
        var fresh = $"fresh-{Guid.NewGuid():N}@t|1.2.3.4";
        dbContext.LoginAttempts.AddRange(
            Attempt(expired, DateTime.UtcNow.AddHours(-25)),
            Attempt(fresh, DateTime.UtcNow.AddHours(-1)));
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.LoginAttempts.AsNoTracking().AnyAsync(a => a.Key == expired))
            .Should().BeFalse("a counter that never decays locks the account out of nowhere");
        (await dbContext.LoginAttempts.AsNoTracking().AnyAsync(a => a.Key == fresh))
            .Should().BeTrue("the attempts inside the window are what the lockout counts");
    }

    [Fact]
    public async Task ExpireTheSecurityTrail()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        dbContext.SecurityAuditEntries.AddRange(
            Audit(expired, userId, DateTime.UtcNow.AddDays(-181)),
            Audit(fresh, userId, DateTime.UtcNow.AddDays(-1)));
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.SecurityAuditEntries.AsNoTracking().AnyAsync(e => e.SecurityAuditEntryId == expired))
            .Should().BeFalse("the trail holds addresses and user agents, and unbounded is a liability");
        (await dbContext.SecurityAuditEntries.AsNoTracking().AnyAsync(e => e.SecurityAuditEntryId == fresh))
            .Should().BeTrue("an incident can still be investigated inside the window");
    }

    [Fact]
    public async Task ExpireNotifications()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        dbContext.Notifications.AddRange(
            Notification(expired, DateTimeOffset.UtcNow.AddDays(-181)),
            Notification(fresh, DateTimeOffset.UtcNow.AddDays(-1)));
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.Notifications.AsNoTracking().AnyAsync(n => n.NotificationId == expired))
            .Should().BeFalse("nothing else ever deletes a notification");
        (await dbContext.Notifications.AsNoTracking().AnyAsync(n => n.NotificationId == fresh))
            .Should().BeTrue("the list is built from the stored rows");
    }

    [Fact]
    public async Task ExpireTombstonedUnreadMarkersAndOnlyThem()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expiredEntity = Guid.NewGuid();
        var freshEntity = Guid.NewGuid();
        var liveEntity = Guid.NewGuid();
        dbContext.UnreadCounters.AddRange(
            Tombstone(expiredEntity, DateTime.UtcNow.AddDays(-8)),
            Tombstone(freshEntity, DateTime.UtcNow.AddHours(-1)),
            // A live marker has no stamp at all, and the sweep's cutoff
            // predicate can never match its NULL.
            new UnreadCounter
            {
                UserId = Guid.Empty,
                EntityId = liveEntity,
                ParentId = liveEntity,
                EntryType = UnreadEntryType.Message,
                LastReadUtc = DateTime.UtcNow.AddDays(-400),
                Counter = 3
            });
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.UnreadCounters.AsNoTracking().AnyAsync(c => c.EntityId == expiredEntity))
            .Should().BeFalse("the tombstone's job is over in minutes; a week is slack");
        (await dbContext.UnreadCounters.AsNoTracking().AnyAsync(c => c.EntityId == freshEntity))
            .Should().BeTrue("a fresh tombstone still refuses to revive its entity");
        (await dbContext.UnreadCounters.AsNoTracking().AnyAsync(c => c.EntityId == liveEntity))
            .Should().BeTrue("a live marker never expires, however old its last read is");
    }

    /// <remarks>
    /// Every row was inserted 30 days ago. Mutation it must fail on: a policy by the moment of
    /// insertion, which takes the waiting row and the row in retry with the rest.
    /// </remarks>
    [Fact]
    public async Task ExpireProcessedAndDeadOutboxRowsAndOnlyThem()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = OutboxRow(processedDaysAgo: 8);
        var fresh = OutboxRow(processedDaysAgo: 1);
        // A row that is not processed has no stamp at all, and the sweep's
        // cutoff predicate can never match its NULL - however old the row grows,
        // only the backlog alert may deal with it.
        var pending = OutboxRow(processedDaysAgo: null);
        var inRetry = OutboxRow(processedDaysAgo: null);
        inRetry.Attempts = 3;
        inRetry.LastError = "TimeoutException: the mail server is down";
        inRetry.NextAttemptUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var longDead = OutboxRow(processedDaysAgo: null);
        longDead.Attempts = 20;
        longDead.DeadUtc = DateTimeOffset.UtcNow.AddDays(-31);
        var recentlyDead = OutboxRow(processedDaysAgo: null);
        recentlyDead.Attempts = 20;
        recentlyDead.DeadUtc = DateTimeOffset.UtcNow.AddDays(-10);
        dbContext.OutboxEvents.AddRange(expired, fresh, pending, inRetry, longDead, recentlyDead);
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == expired.EventId))
            .Should().BeFalse("a processed row's work is done; a week covers the incident review");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == fresh.EventId))
            .Should().BeTrue("a fresh processed row is still evidence in a delivery incident");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == pending.EventId))
            .Should().BeTrue("a waiting event is a delivery still owed, never garbage");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == inRetry.EventId))
            .Should().BeTrue("a row in retry is work still owed, however old it grows");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == longDead.EventId))
            .Should().BeFalse("a month after its death nobody is going to return a dead row");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == recentlyDead.EventId))
            .Should().BeTrue("a dead row stays a month, so it can be looked at and returned");
    }

    // The challenge lives five minutes; the day is slack for a row a login failed to delete.
    [Fact]
    public async Task ExpireAbandonedSecondFactorChallenges()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = Challenge(userId, DateTimeOffset.UtcNow.AddDays(-2));
        var fresh = Challenge(userId, DateTimeOffset.UtcNow.AddMinutes(-1));
        dbContext.TwoFactorChallenges.AddRange(expired, fresh);
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.TwoFactorChallenges.AsNoTracking()
                .AnyAsync(c => c.ChallengeId == expired.ChallengeId))
            .Should().BeFalse("nothing else deletes a challenge whose login was abandoned");
        (await dbContext.TwoFactorChallenges.AsNoTracking()
                .AnyAsync(c => c.ChallengeId == fresh.ChallengeId))
            .Should().BeTrue("a challenge inside its own five minutes is a login in progress");
    }

    private static TwoFactorChallenge Challenge(Guid userId, DateTimeOffset createdUtc) => new()
    {
        ChallengeId = Guid.NewGuid(),
        UserId = userId,
        Account = $"{userId:N}@example.com",
        CreatedUtc = createdUtc,
        ExpiresUtc = createdUtc.AddMinutes(5),
        Persistent = true
    };

    private static DmDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>();

    private static Task Sweep(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IRetentionSweepProcessor>().SweepAsync();

    private async Task<Guid> SeedUser()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            UserId = userId,
            Username = $"r{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static LoginAttempt Attempt(string key, DateTime lastAttemptUtc) => new()
    {
        Key = key,
        Email = key.Split('|')[0],
        IpAddress = "1.2.3.4",
        FailedAttempts = 2,
        LastAttemptUtc = lastAttemptUtc
    };

    private static DbSecurityAuditEntry Audit(Guid id, Guid userId, DateTime timestampUtc) => new()
    {
        SecurityAuditEntryId = id,
        UserId = userId,
        EventType = SecurityEventType.LoginSuccess,
        TimestampUtc = timestampUtc
    };

    private static Notification Notification(Guid id, DateTimeOffset createdUtc) => new()
    {
        NotificationId = id,
        EventType = EventType.NewPost,
        CreatedUtc = createdUtc,
        Metadata = "{}"
    };

    private static DM.Infrastructure.Persistence.Entities.Outbox.OutboxEvent OutboxRow(
        int? processedDaysAgo) => new()
        {
            EventId = Guid.NewGuid(),
            Queue = DM.Infrastructure.Core.Queues.QueueNames.Notifications,
            EventType = EventType.NewGame,
            EntityId = Guid.NewGuid(),
            OccurredUtc = DateTimeOffset.UtcNow.AddDays(-30),
            ProcessedUtc = processedDaysAgo.HasValue
            ? DateTimeOffset.UtcNow.AddDays(-processedDaysAgo.Value)
            : null,
        };

    private static UnreadCounter Tombstone(Guid entityId, DateTime removedUtc) => new()
    {
        UserId = Guid.Empty,
        EntityId = entityId,
        ParentId = entityId,
        EntryType = UnreadEntryType.Message,
        LastReadUtc = removedUtc,
        Counter = 0,
        IsRemoved = true,
        RemovedUtc = removedUtc
    };
}
