using System.Net;
using System.Text.Json;
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

/// <remarks>
/// The list answers from the selection the room list uses, so a starred game has a starred room. Through the
/// HTTP list: the marker passes a viewer-scoped enrichment, a per-user cache and two mappings.
/// </remarks>
public class GameTurnMarkerShould : IntegrationTestBase
{
    private const string ListUrl = "/v1/games?participating=true&projection=ref";

    public GameTurnMarkerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task MarkAGameWaitingForTheViewer()
    {
        await WithWorld("wtura", Awaited.Viewer, async world =>
        {
            var game = await ReadGame(world);

            game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeTrue();
            game.GetProperty("awaitedCharacterNames").EnumerateArray()
                .Select(n => n.GetString())
                .Should().Equal(world.ViewerCharacterName);
        });
    }

    [Fact]
    public async Task LeaveAGameWaitingForSomebodyElseUnmarked()
    {
        await ExpectUnmarked("wturb", Awaited.OtherPlayer);
    }

    // The row stays in the table and the marker goes out — the same moment the room's star does.
    [Fact]
    public async Task LeaveAGameWithAFulfilledExpectationUnmarked()
    {
        await ExpectUnmarked("wturc", Awaited.ViewerFulfilled);
    }

    // Nobody is expected to post at all — the plain case, and the one that would hide a marker
    // stuck on for every row.
    [Fact]
    public async Task LeaveAGameWithoutExpectationsUnmarked()
    {
        await ExpectUnmarked("wturd", Awaited.Nobody);
    }

    /// <remarks>Both answers come from one selection.</remarks>
    [Fact]
    public async Task AgreeWithTheRoomListOfTheSameGame()
    {
        await WithWorld("wture", Awaited.Viewer, async world =>
        {
            (await ReadGame(world)).GetProperty("awaitsViewerTurn").GetBoolean()
                .Should().BeTrue();

            var request = CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/games/{world.GameId}/rooms", Viewer(world));
            var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);

            var pendencies = JsonDocument.Parse(content).RootElement
                .GetProperty("resources").EnumerateArray()
                .SelectMany(room => room.GetProperty("pendencies").EnumerateArray())
                .ToArray();

            pendencies.Should().ContainSingle()
                .Which.GetProperty("characterName").GetString()
                .Should().Be(world.ViewerCharacterName);
        });
    }

    private async Task WithWorld(string publicId, Awaited awaited, Func<World, Task> test)
    {
        var world = await Seed(publicId, awaited);
        try
        {
            await test(world);
        }
        finally
        {
            await Drop(world);
        }
    }

    private Task ExpectUnmarked(string publicId, Awaited awaited) => WithWorld(publicId, awaited, async world =>
    {
        var game = await ReadGame(world);

        game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeFalse();
        game.GetProperty("awaitedCharacterNames").EnumerateArray().Should().BeEmpty();
    });

    private enum Awaited
    {
        Nobody,
        Viewer,
        ViewerFulfilled,
        OtherPlayer,
    }

    private sealed record World(
        Guid GameId,
        Guid MasterId,
        Guid ViewerId,
        string ViewerUsername,
        string ViewerCharacterName,
        Guid OtherPlayerId);

    /// <remarks>The whole list: the marker belongs to a row among the shared fixture's rows.</remarks>
    private async Task<JsonElement> ReadGame(World world)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, ListUrl, Viewer(world));

        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);

        var resources = JsonDocument.Parse(content).RootElement.GetProperty("resources");
        return resources.EnumerateArray()
            .Should().ContainSingle(g => g.GetProperty("id").GetString() == world.GameId.ToString(),
                "the viewer plays in the seeded game, so it is in his participation list")
            .Subject;
    }

    private static GeneralUser Viewer(World world) => new()
    {
        UserId = world.ViewerId,
        Username = world.ViewerUsername,
        Role = UserRole.RegularUser,
    };

    /// <remarks>Both players hold room access: an expectation shows only while its person takes part.</remarks>
    private async Task<World> Seed(string publicId, Awaited awaited)
    {
        var gameId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var viewerCharacterName = $"Ждущий {publicId}";
        var viewerUsername = $"turn{publicId}viewer"[..Math.Min(20, $"turn{publicId}viewer".Length)];
        var now = DateTimeOffset.UtcNow;
        var viewer = new Seat(Guid.NewGuid(), viewerId, viewerCharacterName);
        var other = new Seat(Guid.NewGuid(), otherPlayerId, $"Другой {publicId}");

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.AddRange(
            NewUser(masterId, $"turn{publicId}master"),
            NewUser(viewerId, viewerUsername),
            NewUser(otherPlayerId, $"turn{publicId}other"));
        AddGameWithRoom(dbContext, publicId, gameId, roomId, masterId, now);
        AddCharacters(dbContext, gameId, roomId, now, viewer, other);
        AddPendencies(dbContext, roomId, masterId, awaited, viewer, other, now);

        await dbContext.SaveChangesAsync();

        return new World(gameId, masterId, viewerId, viewerUsername, viewerCharacterName, otherPlayerId);
    }

    private sealed record Seat(Guid CharacterId, Guid PlayerId, string Name);

    private static void AddGameWithRoom(
        DmDbContext dbContext, string publicId, Guid gameId, Guid roomId, Guid masterId, DateTimeOffset now)
    {
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = publicId,
            MasterId = masterId,
            Title = $"Игра с ожиданием хода ({publicId})",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Seeded by GameTurnMarkerShould.",
            CreatedUtc = now.AddDays(-10),
            ActivatedUtc = now.AddDays(-9),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            IsRecruitmentOpen = false,
            RecruitmentCount = 1,
            CommentsAccessMode = CommentsAccessMode.Public,
            IsRemoved = false,
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
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
            IsRemoved = false,
        });
    }

    private static void AddCharacters(
        DmDbContext dbContext, Guid gameId, Guid roomId, DateTimeOffset now, Seat viewer, Seat other)
    {
        dbContext.Set<DbCharacter>().AddRange(
            NewCharacter(viewer.CharacterId, gameId, viewer.PlayerId, viewer.Name, now),
            NewCharacter(other.CharacterId, gameId, other.PlayerId, other.Name, now));

        dbContext.Set<DbRoomAccess>().AddRange(
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = viewer.CharacterId,
                Policy = RoomAccessPolicy.Full,
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = other.CharacterId,
                Policy = RoomAccessPolicy.Full,
            });
    }

    private static void AddPendencies(
        DmDbContext dbContext, Guid roomId, Guid masterId, Awaited awaited, Seat viewer, Seat other, DateTimeOffset now)
    {
        if (awaited == Awaited.Nobody)
        {
            return;
        }

        var forViewer = awaited != Awaited.OtherPlayer;
        dbContext.Set<DbPostPendency>().Add(new DbPostPendency
        {
            PendencyId = Guid.NewGuid(),
            RoomId = roomId,
            CharacterId = forViewer ? viewer.CharacterId : other.CharacterId,
            WaitingForUserId = forViewer ? viewer.PlayerId : other.PlayerId,
            // The master writes the expectations; one created by a stranger
            // is not shown to anybody, which is a different case.
            CreatedById = masterId,
            CreatedUtc = now.AddHours(-3),
            FulfilledUtc = awaited == Awaited.ViewerFulfilled ? now.AddHours(-1) : null,
        });
    }

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username[..Math.Min(20, username.Length)],
        Email = $"{id:N}@example.com",
        PasswordHash = "fakehash",
        Salt = "fakesalt",
        PasswordHashVersion = 2,
        Role = UserRole.RegularUser,
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
        LastActivityUtc = DateTimeOffset.UtcNow,
        IsRemoved = false,
        Status = string.Empty,
        Name = string.Empty,
        Location = string.Empty,
        Info = string.Empty,
    };

    private static DbCharacter NewCharacter(
        Guid characterId, Guid gameId, Guid authorId, string name, DateTimeOffset now) => new()
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = authorId,
            Name = name,
            Status = CharacterStatus.Active,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-8),
            IsRemoved = false,
        };

    private async Task Drop(World world)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "PostPendencies" WHERE "RoomId" IN (SELECT "RoomId" FROM "Rooms" WHERE "GameId" = {0})
            """, world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "RoomAccesses" WHERE "RoomId" IN (SELECT "RoomId" FROM "Rooms" WHERE "GameId" = {0})
            """, world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Characters" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Rooms" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Games" WHERE "GameId" = {0}""", world.GameId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Users" WHERE "UserId" IN ({0}, {1}, {2})""",
            world.MasterId, world.ViewerId, world.OtherPlayerId);
    }
}
