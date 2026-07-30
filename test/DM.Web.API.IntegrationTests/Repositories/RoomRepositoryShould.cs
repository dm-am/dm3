using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Runs against the container Postgres. Update writes a partial patch, so what
/// is being asked is whether the columns it leaves alone survive a real UPDATE
/// statement — a question the InMemory provider, which is LINQ over objects,
/// cannot answer. GetAvailable additionally goes through the accessibility
/// filter, which is relational NULL semantics all the way down.
/// </summary>
public class RoomRepositoryShould : IntegrationTestBase
{
    public RoomRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task PersistIsArchivedWhenUpdateSetsIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var (_, roomId) = await AddGameWithRoomAsync(dbContext, isArchived: false);

        var result = await repository.Update(new UpdateRoomEntity
        {
            RoomId = roomId,
            IsArchived = true,
        });

        result.IsArchived.Should().BeTrue();
        var stored = await dbContext.Rooms.AsNoTracking().FirstAsync(r => r.RoomId == roomId);
        stored.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task KeepStoredIsArchivedWhenUpdateOmitsIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var (_, roomId) = await AddGameWithRoomAsync(dbContext, isArchived: true);

        var result = await repository.Update(new UpdateRoomEntity
        {
            RoomId = roomId,
            Title = "Renamed Room",
            IsArchived = null,
        });

        // A null in the patch means "not mentioned", not "set to false". Getting
        // this wrong un-archives every room anyone renames.
        result.IsArchived.Should().BeTrue();
        result.Title.Should().Be("Renamed Room");
        var stored = await dbContext.Rooms.AsNoTracking().FirstAsync(r => r.RoomId == roomId);
        stored.IsArchived.Should().BeTrue();
        stored.Title.Should().Be("Renamed Room");
    }

    [Fact]
    public async Task KeepArchivedRoomRetrievable()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var (masterId, roomId) = await AddGameWithRoomAsync(dbContext, isArchived: true);

        // Archiving hides a room from the room list, not from its own URL: the
        // history stays readable to whoever could read it before.
        var room = await repository.GetAvailable(roomId, masterId);

        room.Should().NotBeNull();
        room!.IsArchived.Should().BeTrue();
    }

    /// <summary>
    /// A fresh game and room per test: the fixture's database is shared and
    /// seeded, so fixed identifiers would collide across tests and with the seed.
    /// </summary>
    private static async Task<(Guid MasterId, Guid RoomId)> AddGameWithRoomAsync(
        DmDbContext dbContext, bool isArchived)
    {
        var masterId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

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
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = UniquePublicId(),
            Title = "Game for the room archive check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Test Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
            IsArchived = isArchived,
        });
        await dbContext.SaveChangesAsync();

        return (masterId, roomId);
    }

    /// <summary>
    /// A public id no other row holds. The column is NOT NULL and uniquely
    /// indexed — a constraint the InMemory provider did not have, which is why
    /// the version of this test that ran there could omit the value entirely.
    /// </summary>
    private static string UniquePublicId() => Guid.NewGuid().ToString("N")[..10];
}
