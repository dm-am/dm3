using System;
using System.Threading.Tasks;
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
/// The retention sweep replaces the TTL indexes of the retired document store:
/// one pass per policy, DELETE by the stamped column. Each of the policies is
/// asserted the same way — a row older than the term goes, a row younger
/// stays (AC-5). Against a live Postgres because the sweep is raw SQL over the
/// registry, and nothing else executes it.
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

    [Fact]
    public async Task ExpirePublishedOutboxRowsAndOnlyThem()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = Context(scope);
        var expired = OutboxRow(publishedDaysAgo: 8);
        var fresh = OutboxRow(publishedDaysAgo: 1);
        // An unpublished row has no stamp at all, and the sweep's cutoff
        // predicate can never match its NULL - however old the row grows, only
        // the backlog alert may deal with it (AC-6, INV-6).
        var pending = OutboxRow(publishedDaysAgo: null);
        dbContext.OutboxEvents.AddRange(expired, fresh, pending);
        await dbContext.SaveChangesAsync();

        await Sweep(scope);

        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == expired.EventId))
            .Should().BeFalse("a published row's work is done; a week covers the incident review");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == fresh.EventId))
            .Should().BeTrue("a fresh published row is still evidence in a delivery incident");
        (await dbContext.OutboxEvents.AsNoTracking().AnyAsync(e => e.EventId == pending.EventId))
            .Should().BeTrue("an unpublished event is a delivery still owed, never garbage");
    }

    /// <summary>
    /// A login that stopped between the two factors is a stream with a term.
    /// </summary>
    /// <remarks>
    /// The challenge itself lives five minutes; the day the sweep measures is
    /// slack for a row the end of a login failed to delete, and not a second
    /// lifetime for the challenge.
    /// </remarks>
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
        int? publishedDaysAgo) => new()
        {
            EventId = Guid.NewGuid(),
            EventType = EventType.NewGame,
            EntityId = Guid.NewGuid(),
            OccurredUtc = DateTimeOffset.UtcNow.AddDays(-30),
            PublishedUtc = publishedDaysAgo.HasValue
            ? DateTimeOffset.UtcNow.AddDays(-publishedDaysAgo.Value)
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
