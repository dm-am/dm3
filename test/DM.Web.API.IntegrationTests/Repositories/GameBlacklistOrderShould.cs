using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Blacklists;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameBlacklist = DM.Infrastructure.Persistence.Entities.Game.Links.GameBlacklist;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A game's blacklist is read oldest entry first, and entries made at the same
/// moment are read by name.
/// </summary>
/// <remarks>
/// A name just added belongs at the bottom of the list, and a read without an
/// order gave the database the choice: after a removal Postgres may hand the
/// rows back in any order it likes. The copy of the master's personal list
/// writes one moment on every row, so the date alone does not settle the order
/// either.
///
/// The rows are written in the order no read should return them: newest first,
/// and of the two made together the later name first. The blocked users'
/// identifiers rise in insertion order too, so neither the heap, the index on
/// (GameId, BlockedUserId) nor the users table can hand back the expected order
/// by accident.
/// </remarks>
public class GameBlacklistOrderShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Earlier = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    public GameBlacklistOrderShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReadTheListByTheMomentOfEntryThenByName()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameBlacklistRepository>();

        var (masterId, gameId) = await AddGameAsync(dbContext);

        // One prefix for the three names, so that only the last letter orders them
        // and every collation agrees on that order.
        var prefix = $"gbo{Guid.NewGuid():N}"[..19];
        var userIds = Enumerable.Range(0, 3)
            .Select(_ => Guid.NewGuid())
            .OrderBy(id => id.ToString("N"), StringComparer.Ordinal)
            .ToArray();

        var newest = await AddUserAsync(dbContext, userIds[0], prefix + "a");
        var laterName = await AddUserAsync(dbContext, userIds[1], prefix + "c");
        var earlierName = await AddUserAsync(dbContext, userIds[2], prefix + "b");

        // One save per row, so the heap holds them in exactly this order: a batch
        // would be free to sort its inserts by key.
        await AddEntryAsync(dbContext, gameId, newest, masterId, Later);
        await AddEntryAsync(dbContext, gameId, laterName, masterId, Earlier);
        await AddEntryAsync(dbContext, gameId, earlierName, masterId, Earlier);

        var list = await repository.Get(gameId);

        list.Select(u => u.UserId).Should().Equal(
            [earlierName, laterName, newest],
            "the list is read by the moment of entry, and by name where that moment is the same");
    }

    private static async Task AddEntryAsync(
        DmDbContext dbContext, Guid gameId, Guid blockedUserId, Guid blockedByUserId, DateTimeOffset createdUtc)
    {
        dbContext.GameBlacklists.Add(new DbGameBlacklist
        {
            EntryId = Guid.NewGuid(),
            GameId = gameId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = createdUtc
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext, Guid userId, string username)
    {
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>A game with the master it needs to exist.</summary>
    private static async Task<(Guid MasterId, Guid GameId)> AddGameAsync(DmDbContext dbContext)
    {
        var masterId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated
        // rather than used whole.
        await AddUserAsync(dbContext, masterId, $"gbm{masterId:N}"[..20]);
        var gameId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the blacklist order check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();

        return (masterId, gameId);
    }
}
