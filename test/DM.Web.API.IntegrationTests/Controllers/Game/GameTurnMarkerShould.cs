using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
/// A game in the participation list says whether it is waiting for a post from
/// the reader of the list.
/// </summary>
/// <remarks>
/// The room list has carried post pendencies from the start and the sidebar
/// draws a star from them, but a list of games carries no rooms: the mentor
/// panel got its stars by fetching every room of every curated game, and "Мои
/// игры", which is the panel a player actually watches, had no marker at all.
/// The list now answers it itself, from the same selection the room list uses —
/// so a starred game is a game with a starred room inside it, and the two
/// screens cannot disagree.
///
/// Asserted through the HTTP list rather than against the repository because
/// the marker is a promise of the payload: it travels through a viewer-scoped
/// enrichment, a per-user cache and two mapping profiles, and each of them has
/// dropped a viewer-scoped field before.
/// </remarks>
public class GameTurnMarkerShould : IntegrationTestBase
{
    private const string ListUrl = "/v1/games?participating=true&projection=ref";

    public GameTurnMarkerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The expectation names the viewer's own character, so his list marks the game
    /// and names who is being waited for.
    /// </summary>
    [Fact]
    public async Task MarkAGameWaitingForTheViewer()
    {
        var world = await Seed("wtura", Awaited.Viewer);
        try
        {
            var game = await ReadGame(world);

            game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeTrue();
            game.GetProperty("awaitedCharacterNames").EnumerateArray()
                .Select(n => n.GetString())
                .Should().Equal(world.ViewerCharacterName);
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// The same game, the same room, the same master — the turn is another
    /// player's. The viewer plays in the game and sees it in his list, unmarked.
    /// </summary>
    [Fact]
    public async Task LeaveAGameWaitingForSomebodyElseUnmarked()
    {
        var world = await Seed("wturb", Awaited.OtherPlayer);
        try
        {
            var game = await ReadGame(world);

            game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeFalse();
            game.GetProperty("awaitedCharacterNames").EnumerateArray().Should().BeEmpty();
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// The post that answered the expectation has landed. The row stays in the
    /// table and the marker goes out — the same moment the room's star does.
    /// </summary>
    [Fact]
    public async Task LeaveAGameWithAFulfilledExpectationUnmarked()
    {
        var world = await Seed("wturc", Awaited.ViewerFulfilled);
        try
        {
            var game = await ReadGame(world);

            game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeFalse();
            game.GetProperty("awaitedCharacterNames").EnumerateArray().Should().BeEmpty();
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// Nobody is expected to post at all — the plain case, and the one that would
    /// hide a marker stuck on for every row.
    /// </summary>
    [Fact]
    public async Task LeaveAGameWithoutExpectationsUnmarked()
    {
        var world = await Seed("wturd", Awaited.Nobody);
        try
        {
            var game = await ReadGame(world);

            game.GetProperty("awaitsViewerTurn").GetBoolean().Should().BeFalse();
            game.GetProperty("awaitedCharacterNames").EnumerateArray().Should().BeEmpty();
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// The marked game and the room inside it name the same expectation.
    /// </summary>
    /// <remarks>
    /// The two answers come from one selection, and this is what says so: a game
    /// list that marks a game whose rooms show nothing would send the reader to
    /// look for a turn that is not there.
    /// </remarks>
    [Fact]
    public async Task AgreeWithTheRoomListOfTheSameGame()
    {
        var world = await Seed("wture", Awaited.Viewer);
        try
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
        }
        finally
        {
            await Drop(world);
        }
    }

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

    /// <summary>
    /// Read the seeded game out of the viewer's own participation list.
    /// </summary>
    /// <remarks>
    /// The whole list, not a filtered read: the marker belongs to a row among
    /// rows, and the games of the shared fixture ride along in the same response.
    /// </remarks>
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

    /// <summary>
    /// An active game with one room, a master, the viewer with a character, a
    /// second player with his own, and at most one expectation.
    /// </summary>
    /// <remarks>
    /// Both players hold room access: the expectation is only shown when the
    /// person it awaits still takes part in the room, and the case worth
    /// separating here is who is awaited, not who may enter.
    /// </remarks>
    private async Task<World> Seed(string publicId, Awaited awaited)
    {
        var gameId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var viewerCharacterId = Guid.NewGuid();
        var otherCharacterId = Guid.NewGuid();
        var viewerCharacterName = $"Ждущий {publicId}";
        var viewerUsername = $"turn{publicId}viewer"[..Math.Min(20, $"turn{publicId}viewer".Length)];
        var now = DateTimeOffset.UtcNow;

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.AddRange(
            NewUser(masterId, $"turn{publicId}master"),
            NewUser(viewerId, viewerUsername),
            NewUser(otherPlayerId, $"turn{publicId}other"));

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

        dbContext.Set<DbCharacter>().AddRange(
            NewCharacter(viewerCharacterId, gameId, viewerId, viewerCharacterName, now),
            NewCharacter(otherCharacterId, gameId, otherPlayerId, $"Другой {publicId}", now));

        dbContext.Set<DbRoomAccess>().AddRange(
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = viewerCharacterId,
                Policy = RoomAccessPolicy.Full,
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = otherCharacterId,
                Policy = RoomAccessPolicy.Full,
            });

        if (awaited != Awaited.Nobody)
        {
            var forViewer = awaited != Awaited.OtherPlayer;
            dbContext.Set<DbPostPendency>().Add(new DbPostPendency
            {
                PendencyId = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = forViewer ? viewerCharacterId : otherCharacterId,
                WaitingForUserId = forViewer ? viewerId : otherPlayerId,
                // The master writes the expectations; one created by a stranger
                // is not shown to anybody, which is a different case.
                CreatedById = masterId,
                CreatedUtc = now.AddHours(-3),
                FulfilledUtc = awaited == Awaited.ViewerFulfilled ? now.AddHours(-1) : null,
            });
        }

        await dbContext.SaveChangesAsync();

        return new World(gameId, masterId, viewerId, viewerUsername, viewerCharacterName, otherPlayerId);
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
