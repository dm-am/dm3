using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Blacklists;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Repositories;

// Adding is check-then-insert, so the rule lives in the schema, and the repository answers its
// refusal in the domain's terms.
public class GameBlacklistUniquenessShould : IntegrationTestBase
{
    public GameBlacklistUniquenessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondEntryForOneUserOnOneGame()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameBlacklistRepository>();

        var (masterId, gameId) = await AddGameAsync(dbContext);
        var blockedUserId = await AddUserAsync(dbContext);

        await repository.Add(gameId, blockedUserId, masterId);

        // Another entry identifier, the same pair: the insert the loser of the race
        // issues after its own check came back empty.
        Func<Task> second = () => repository.Add(gameId, blockedUserId, masterId);

        var refusal = await second.Should().ThrowAsync<DuplicateEntityException>(
            "one user is on one game's list once, whatever identifier the row carries");
        refusal.And.InnerException.Should().BeOfType<DbUpdateException>()
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505",
                "the refusal is the index answering the insert, not a read answering it");

        (await dbContext.GameBlacklists.CountAsync(
                b => b.GameId == gameId && b.BlockedUserId == blockedUserId))
            .Should().Be(1);
    }

    // The delete names the pair and accepts zero rows: the row can be gone by the time it runs.
    [Fact]
    public async Task RemoveAPairThatIsNotOnTheList()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameBlacklistRepository>();

        var (masterId, gameId) = await AddGameAsync(dbContext);
        var blockedUserId = await AddUserAsync(dbContext);

        await repository.Add(gameId, blockedUserId, masterId);
        await repository.Remove(gameId, blockedUserId);

        Func<Task> again = () => repository.Remove(gameId, blockedUserId);

        await again.Should().NotThrowAsync(
            "the pair is already off the list, which is the outcome the caller asked for");
        (await dbContext.GameBlacklists.CountAsync(
                b => b.GameId == gameId && b.BlockedUserId == blockedUserId))
            .Should().Be(0);
    }

    // The copies stand in for any write landing between the read and the insert; each ends without
    // an error.
    [Fact]
    public async Task CopyThePersonalListOnceWhenCopiesRunTogether()
    {
        const int entries = 20;
        const int copies = 8;

        Guid masterId;
        Guid gameId;
        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            (masterId, gameId) = await AddGameAsync(dbContext);
            for (var i = 0; i < entries; i++)
            {
                dbContext.UserBlacklists.Add(new DbUserBlacklist
                {
                    EntryId = Guid.NewGuid(),
                    OwnerId = masterId,
                    BlockedUserId = await AddUserAsync(dbContext),
                    CreatedUtc = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var written = await Task.WhenAll(Enumerable.Range(0, copies).Select(async _ =>
        {
            // One scope per copy: a DbContext is scoped and must not be shared
            // between parallel calls.
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGameBlacklistRepository>();
            return await repository.CopyFromPersonalBlacklist(gameId, masterId);
        }));

        written.Sum().Should().Be(entries, "each entry is written by exactly one of the copies");

        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            (await dbContext.GameBlacklists.CountAsync(b => b.GameId == gameId)).Should().Be(entries);
        }
    }

    // A user of this test's own: the fixture database is shared and seeded, so a fixed identifier
    // would depend on whoever else wrote one.
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"gbl{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static async Task<(Guid MasterId, Guid GameId)> AddGameAsync(DmDbContext dbContext)
    {
        var masterId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the blacklist uniqueness check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();

        return (masterId, gameId);
    }
}
