using System.Data.Common;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Repositories;

// A game open without its master's list lets the people on it apply and comment, so the copy and the
// game commit together or not at all.
public class GameCreationBlacklistCopyShould : IntegrationTestBase
{
    public GameCreationBlacklistCopyShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CommitTheMastersBlacklistWithTheGame()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var (masterId, blockedId) = await SeedMasterWithBlacklistAsync(dbContext);
        var (game, room) = NewGame(masterId);

        await repository.Create(game, room);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.GameBlacklists.Where(b => b.GameId == game.GameId).Select(b => b.BlockedUserId).ToArrayAsync())
            .Should().Equal(blockedId);
    }

    [Fact]
    public async Task LeaveNoGameWhenTheCopyFails()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var (masterId, _) = await SeedMasterWithBlacklistAsync(
            scope.ServiceProvider.GetRequiredService<DmDbContext>());
        var (game, room) = NewGame(masterId);

        await using var refusingContext = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(DatabaseFixture.ConnectionString)
            .AddInterceptors(new RefuseBlacklistCopy())
            .Options);
        var repository = new GameRepository(
            refusingContext,
            scope.ServiceProvider.GetRequiredService<IGuidFactory>(),
            scope.ServiceProvider.GetRequiredService<IDateTimeProvider>(),
            scope.ServiceProvider.GetRequiredService<PublicIdService>());

        var act = () => repository.Create(game, room);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(RefuseBlacklistCopy.Refusal);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Games.IgnoreQueryFilters().AnyAsync(g => g.GameId == game.GameId))
            .Should().BeFalse("the game row rolls back with the copy that failed after it");
        (await db.Rooms.IgnoreQueryFilters().AnyAsync(r => r.RoomId == room.RoomId)).Should().BeFalse();
    }

    private static (CreateGameEntity Game, CreateRoomEntity Room) NewGame(Guid masterId)
    {
        var gameId = Guid.NewGuid();
        var game = new CreateGameEntity
        {
            GameId = gameId,
            MasterId = masterId,
            Title = "Game for the blacklist copy check",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Info",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            CopyMasterBlacklist = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        var room = new CreateRoomEntity
        {
            RoomId = Guid.NewGuid(),
            GameId = gameId,
            Title = "Игровая",
            Type = RoomType.Default,
            AccessType = RoomAccessType.Open,
            ViewDiceResults = true,
            DiceEnabled = true
        };
        return (game, room);
    }

    private static async Task<(Guid MasterId, Guid BlockedId)> SeedMasterWithBlacklistAsync(DmDbContext dbContext)
    {
        var masterId = AddUser(dbContext);
        var blockedId = AddUser(dbContext);
        await dbContext.SaveChangesAsync();
        dbContext.UserBlacklists.Add(new DbUserBlacklist
        {
            EntryId = Guid.NewGuid(),
            OwnerId = masterId,
            BlockedUserId = blockedId,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return (masterId, blockedId);
    }

    private static Guid AddUser(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is truncated.
            Username = $"gcc{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        return userId;
    }

    // Fails the copy statement alone, after the game row is already written in the same transaction.
    private sealed class RefuseBlacklistCopy : DbCommandInterceptor
    {
        public const string Refusal = "blacklist copy refused";

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO \"GameBlacklists\"")
                ? throw new InvalidOperationException(Refusal)
                : base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
