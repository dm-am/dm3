using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// "Ждем пост" is created from the site, with the body the site sends.
/// </summary>
/// <remarks>
/// It never was. The endpoint bound the response DTO, whose author is a required
/// nested user, and the site sends the character and nothing else: every press
/// answered 400 "CreatedBy field is required" before the service was reached.
/// Behind the binding the service wanted the username of the player as well.
/// Both are the server's own facts - the caller is in the session, the player
/// stands behind the character - so the body is the character and the server
/// reads the rest.
///
/// The body is written out as a string here, the way the client serializes it,
/// rather than built from a server type: a test that sends what the server
/// declares passes whatever the client actually sends.
/// </remarks>
public class PostPendencyCreationShould : IntegrationTestBase
{
    public PostPendencyCreationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task WaitForThePlayerOfTheCharacterTheMasterNamed()
    {
        var world = await Seed();
        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Post, $"/v1/rooms/{world.RoomId}/pendencies", world.Master);
            request.Content = new StringContent(
                $$"""{"characterId":"{{world.CharacterId}}"}""", Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
            var pendency = JsonDocument.Parse(content).RootElement.GetProperty("resource");
            pendency.GetProperty("characterId").GetString().Should().Be(world.CharacterId.ToString());
            pendency.GetProperty("waitingFor").GetProperty("username").GetString()
                .Should().Be(world.PlayerUsername, "the one waited for is the player of the character");
            pendency.GetProperty("createdBy").GetProperty("username").GetString()
                .Should().Be(world.Master.Username, "the one who waits is whoever asked");

            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<DmDbContext>()
                .Set<DbPostPendency>().Where(p => p.RoomId == world.RoomId).ToListAsync();
            stored.Should().ContainSingle()
                .Which.Should().Match<DbPostPendency>(p =>
                    p.CharacterId == world.CharacterId &&
                    p.WaitingForUserId == world.PlayerId &&
                    p.CreatedById == world.Master.UserId);
        }
        finally
        {
            await Drop(world);
        }
    }

    private sealed record World(
        Guid GameId,
        Guid RoomId,
        Guid CharacterId,
        GeneralUser Master,
        Guid PlayerId,
        string PlayerUsername);

    /// <summary>
    /// An active game with one room, its master, and a player whose character
    /// the room lets in.
    /// </summary>
    private async Task<World> Seed()
    {
        var gameId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Username is varchar(20) and uniquely indexed, so the id is truncated.
        var masterName = $"pdm{masterId:N}"[..20];
        var playerName = $"pdp{playerId:N}"[..20];

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.AddRange(NewUser(masterId, masterName), NewUser(playerId, playerName));

        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = "Game for the post pendency check",
            CreatedUtc = now.AddDays(-10),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });

        dbContext.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Игровая",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            OrderNumber = 1.0,
        });

        dbContext.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = playerId,
            Name = "Ожидаемый " + Guid.NewGuid().ToString("N")[..6],
            Status = CharacterStatus.Active,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-8),
        });

        dbContext.Set<DbRoomAccess>().Add(new DbRoomAccess
        {
            AccessId = Guid.NewGuid(),
            RoomId = roomId,
            CharacterId = characterId,
            Policy = RoomAccessPolicy.Full,
        });

        await dbContext.SaveChangesAsync();

        var master = new GeneralUser { UserId = masterId, Username = masterName, Role = UserRole.RegularUser };
        return new World(gameId, roomId, characterId, master, playerId, playerName);
    }

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{id:N}@pendency.example",
        PasswordHash = "hash",
        Salt = "salt",
    };

    private async Task Drop(World world)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "PostPendencies" WHERE "RoomId" = {0}""", world.RoomId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "RoomAccesses" WHERE "RoomId" = {0}""", world.RoomId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Characters" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Rooms" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Games" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Users" WHERE "UserId" IN ({0}, {1})""", world.Master.UserId, world.PlayerId);
    }
}
