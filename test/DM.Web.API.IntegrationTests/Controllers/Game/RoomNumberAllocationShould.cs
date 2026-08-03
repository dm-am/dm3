using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// RoomNumber is the address of a room: every link to one is
/// /games/{game}/rooms/{number} and the number is what the address resolves by.
/// No write path filled it in, so every room the application built carried zero
/// and all rooms of a game shared one address - the seed, which writes its
/// numbers by hand, was the only place the feature looked like it worked.
/// The number is allocated as MAX+1, which two creates read alike unless
/// creation is serialized per game.
/// </summary>
public class RoomNumberAllocationShould : IntegrationTestBase
{
    private const int Parallelism = 8;

    public RoomNumberAllocationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The room a game is created with is written by the game repository, in the
    /// same batch as the game, and is the one room no allocation pass ever sees.
    /// </summary>
    [Fact]
    public async Task NumberTheRoomAGameIsCreatedWith()
    {
        var (gameId, masterId) = await CreateGame();
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

            var numbers = await dbContext.Rooms
                .Where(r => r.GameId == gameId)
                .Select(r => r.RoomNumber)
                .ToArrayAsync();

            numbers.Should().ContainSingle().Which.Should().Be(1);
        }
        finally
        {
            await DropGame(gameId, masterId);
        }
    }

    [Fact]
    public async Task GiveConcurrentRoomsOfOneGameDistinctNumbers()
    {
        var (gameId, masterId) = await CreateGame();
        try
        {
            var rooms = await Task.WhenAll(Enumerable
                .Range(0, Parallelism)
                .Select(i => CreateRoom(gameId, i)));

            // The game came with its own first room, so the numbers handed out
            // here start at two.
            rooms.Select(r => r.RoomNumber).Should().BeEquivalentTo(Enumerable.Range(2, Parallelism));
        }
        finally
        {
            await DropGame(gameId, masterId);
        }
    }

    [Fact]
    public async Task RejectADuplicateNumberInTheSameGame()
    {
        var (gameId, masterId) = await CreateGame();
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var insertDuplicate = async () => await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Rooms" ("RoomId", "GameId", "RoomNumber", "Title", "AccessType", "Type",
                                     "OrderNumber", "ViewPrivateText", "ViewDiceResults", "DiceEnabled",
                                     "IsArchived", "IsRemoved")
                VALUES ({0}, {1}, 1, 'duplicate', 0, 0, 1, false, false, false, false, false)
                """,
                Guid.NewGuid(), gameId);

            (await insertDuplicate.Should().ThrowAsync<PostgresException>())
                .Which.ConstraintName.Should().Be("IX_Rooms_GameId_RoomNumber");
        }
        finally
        {
            await DropGame(gameId, masterId);
        }
    }

    /// <summary>
    /// A removed room keeps its number: its address has to stay dead rather than
    /// start opening a room written after it.
    /// </summary>
    [Fact]
    public async Task NotHandOutTheNumberOfARemovedRoom()
    {
        var (gameId, masterId) = await CreateGame();
        try
        {
            var removed = await CreateRoom(gameId, 0);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
                await dbContext.Rooms
                    .Where(r => r.RoomId == removed.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRemoved, true));
            }

            var next = await CreateRoom(gameId, 1);

            next.RoomNumber.Should().Be(removed.RoomNumber + 1);
        }
        finally
        {
            await DropGame(gameId, masterId);
        }
    }

    private async Task<Room> CreateRoom(Guid gameId, int index)
    {
        // One scope per concurrent create: a DbContext is scoped and must not be
        // shared between parallel calls.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        return await repository.Create(new CreateRoomEntity
        {
            RoomId = Guid.NewGuid(),
            GameId = gameId,
            Title = $"Concurrent room {index}",
            Type = RoomType.Default,
            AccessType = RoomAccessType.Open,
            OrderNumber = index + 2,
        });
    }

    /// <summary>
    /// A game built by the repository the application creates games with, master
    /// included: the number of the room it comes with is one of the facts here.
    /// </summary>
    private async Task<(Guid GameId, Guid MasterId)> CreateGame()
    {
        var gameId = Guid.NewGuid();
        var masterId = Guid.NewGuid();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // Games.MasterId carries a foreign key, so the master has to exist.
        // Username is varchar(20) and uniquely indexed.
        dbContext.Users.Add(new DbUser
        {
            UserId = masterId,
            Username = $"room{masterId:N}"[..20],
            Email = $"{masterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        await repository.Create(
            new CreateGameEntity
            {
                GameId = gameId,
                MasterId = masterId,
                Title = "Game for the room numbering check",
                Status = ModuleStatus.Active,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            new CreateRoomEntity
            {
                RoomId = Guid.NewGuid(),
                GameId = gameId,
                Title = "Игровая",
                Type = RoomType.Default,
                AccessType = RoomAccessType.Open,
                OrderNumber = 1,
            });

        return (gameId, masterId);
    }

    private async Task DropGame(Guid gameId, Guid masterId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        // The rooms of a game point at each other, so they go in one statement:
        // PostgreSQL checks the self reference once the whole DELETE is done.
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Rooms" WHERE "GameId" = {0}""", gameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Games" WHERE "GameId" = {0}""", gameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Users" WHERE "UserId" = {0}""", masterId);
    }
}
