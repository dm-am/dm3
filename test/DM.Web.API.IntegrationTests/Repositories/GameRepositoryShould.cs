using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Runs against the container Postgres because the subscriber summary is one
/// relational statement: the count is a GROUP BY, the preview is a window
/// function with a LIMIT inside a partition, and the viewer flag is an EXISTS.
/// None of those three is exercised by an in-memory provider, and the ordering
/// the preview depends on is a Postgres null-placement question.
/// </summary>
public class GameRepositoryShould : IntegrationTestBase
{
    private const int SubscriberCount = 25;

    public GameRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The preview used to be an arbitrary twenty of however many subscribers a
    /// game had, taken in memory after every row was loaded. It is now the twenty
    /// most recently active, ordered, chosen by the database — and the total is
    /// still the total, not the size of the preview.
    /// </summary>
    [Fact]
    public async Task ReportEverySubscriberInTheCountAndOnlyTheMostRecentInThePreview()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (gameId, masterId, subscribers) = await SeedGameWithSubscribersAsync(dbContext);

        var game = await repository.GetGame(gameId, masterId, mayJudgePremoderation: false);

        game.Should().NotBeNull();
        game!.SubscribersCount.Should().Be(SubscriberCount,
            "the count is the total, and the preview cap must not truncate it");
        game.SubscriberUsernames.Should().HaveCount(SubscriptionPolicy.PreviewCap);

        // Seeded newest-activity-first, so the expected preview is the head of
        // that list — a set comparison would pass on any twenty of the 25.
        game.SubscriberUsernames.Should().Equal(
            subscribers.Take(SubscriptionPolicy.PreviewCap).Select(s => s.Username),
            "the preview is the most recently active subscribers, newest first");
    }

    /// <summary>
    /// The Reader role is the only thing the subscriber id set was consulted for
    /// in authorization. It now comes from a per-viewer flag, so it has to answer
    /// for the subscriber and only for the subscriber.
    /// </summary>
    [Fact]
    public async Task ResolveTheReaderRoleForASubscriberAndNotForAStranger()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (gameId, masterId, subscribers) = await SeedGameWithSubscribersAsync(dbContext);
        var subscriberId = subscribers[0].UserId;
        var (strangerId, _) = await AddUserAsync(dbContext, "stranger", DateTimeOffset.UtcNow);

        var asSubscriber = (await repository.GetGame(gameId, subscriberId, mayJudgePremoderation: false))!;
        var asStranger = (await repository.GetGame(gameId, strangerId, mayJudgePremoderation: false))!;
        var asMaster = (await repository.GetGame(gameId, masterId, mayJudgePremoderation: false))!;

        asSubscriber.GetRoles(subscriberId).Should().Contain(GameRole.Reader);
        asStranger.GetRoles(strangerId).Should().BeEmpty();

        // The master subscribes to nothing here, so the flag must not leak the
        // fact that somebody else does — the count says twenty-five either way.
        asMaster.GetRoles(masterId).Should().Equal(GameRole.Master);
        asMaster.SubscribersCount.Should().Be(SubscriberCount);
    }

    /// <summary>
    /// A fresh game and a fresh set of subscribers per test: the fixture's
    /// database is shared, so seeding onto the seed's own game would make the
    /// count depend on what other tests subscribed to it.
    /// </summary>
    /// <returns>
    /// The game, its master, and its subscribers ordered the way the preview is
    /// expected to be — most recently active first.
    /// </returns>
    private static async Task<(Guid GameId, Guid MasterId, List<(Guid UserId, string Username)> Subscribers)>
        SeedGameWithSubscribersAsync(DmDbContext dbContext)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (masterId, _) = await AddUserAsync(dbContext, $"m{suffix}", DateTimeOffset.UtcNow);

        var gameId = Guid.NewGuid();
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = $"g{suffix}",
            MasterId = masterId,
            Title = $"Subscriber summary game {suffix}",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await dbContext.SaveChangesAsync();

        // Distinct last-activity moments, one minute apart, newest first. Equal
        // timestamps would let the subscription-id tie-break decide the order and
        // the assertion would be about insertion order instead of activity.
        var now = DateTimeOffset.UtcNow;
        var subscribers = new List<(Guid UserId, string Username)>();
        for (var i = 0; i < SubscriberCount; i++)
        {
            var (userId, username) = await AddUserAsync(dbContext, $"s{i:D2}{suffix}", now.AddMinutes(-i));
            subscribers.Add((userId, username));

            dbContext.Set<DbSubscription>().Add(new DbSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                SubscriberId = userId,
                TargetType = SubscriptionTargetType.Game,
                TargetId = gameId,
                Settings = SubscriptionSettings.None,
                CreatedUtc = now,
            });
        }
        await dbContext.SaveChangesAsync();

        return (gameId, masterId, subscribers);
    }

    /// <returns>
    /// The id and the username as stored — truncated, so the caller compares
    /// against what the repository will actually return rather than the prefix it
    /// asked for.
    /// </returns>
    private static async Task<(Guid UserId, string Username)> AddUserAsync(
        DmDbContext dbContext, string prefix, DateTimeOffset lastActivityUtc)
    {
        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id pads the prefix
        // rather than being used whole.
        var username = $"{prefix}{userId:N}"[..20];
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = lastActivityUtc,
        });
        await dbContext.SaveChangesAsync();
        return (userId, username);
    }
}
