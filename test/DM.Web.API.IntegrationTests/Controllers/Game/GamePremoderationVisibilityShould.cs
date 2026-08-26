using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Who may open a game that is waiting on a premoderation verdict, end to end:
/// the SQL scope, the read intention and the controller, which is the only place
/// the three can be seen answering as one.
/// </summary>
/// <remarks>
/// The scope hands a premoderated game to its leads, its invitees and its
/// assigned curator. A game in AwaitingEdits has no curator recorded — the status
/// every newbie's game is created in — so every rank that may pass the verdict,
/// the mentor included, was answered 404 on the very games the moderation queue
/// links to, and the verdict buttons were drawn inside a page that never
/// rendered. The rank arm has to open exactly these games and nothing else,
/// which is why a private draft and a removed game are asserted beside them.
/// </remarks>
public class GamePremoderationVisibilityShould : IntegrationTestBase
{
    public GamePremoderationVisibilityShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public async Task OpenAGameAwaitingEditsToEveryRankThatMayJudgeIt(UserRole role)
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.AwaitingEditsPublicId, await UserAsync(role));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "{0} may pass the verdict on this game, so the game has to open for them", role);
    }

    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public async Task OpenAGameAwaitingApprovalToEveryRankThatMayJudgeIt(UserRole role)
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.AwaitingApprovalPublicId, await UserAsync(role));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HideAPremoderatedGameFromAGuest()
    {
        var seeded = await SeedAsync();

        var response = await Client.GetAsync(Url(seeded.AwaitingEditsPublicId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HideAPremoderatedGameFromAnOrdinaryUser()
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.AwaitingEditsPublicId, await UserAsync(UserRole.RegularUser));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Playing somebody else's game is a role in that game and a claim about no
    /// other: the reader is a stranger here and the premoderated game stays shut.
    /// </summary>
    [Fact]
    public async Task HideAPremoderatedGameFromAPlayerOfAnotherGame()
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.AwaitingEditsPublicId, seeded.ForeignPlayer);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The master of the game is admitted by their own role, not by the rank, and
    /// that has to keep working: the arm is an addition to the scope and not a
    /// replacement of it.
    /// </summary>
    [Fact]
    public async Task KeepAPremoderatedGameOpenToItsOwnMaster()
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.AwaitingEditsPublicId, seeded.Master);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A private draft is hidden by its author's choice and not by premoderation.
    /// The rank is keyed on the premoderation status alone, so it must not reach
    /// the draft — an arm keyed on "the game is hidden" would.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public async Task HideAPrivateDraftFromTheRanksThatJudgePremoderation(UserRole role)
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.PrivateDraftPublicId, await UserAsync(role));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A removed game is gone whatever verdict it was waiting on: the soft-delete
    /// flag guards the whole scope and the rank does not reopen it.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Admin)]
    public async Task HideARemovedPremoderatedGameFromTheRanksThatJudgePremoderation(UserRole role)
    {
        var seeded = await SeedAsync();

        var response = await GetDetails(seeded.RemovedPublicId, await UserAsync(role));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Url(string publicId) => $"/v1/games/{publicId}/details";

    private Task<HttpResponseMessage> GetDetails(string publicId, GeneralUser reader) =>
        Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, Url(publicId), reader));

    /// <summary>A reader holding the given site-wide role and no role in any game.</summary>
    private async Task<GeneralUser> UserAsync(UserRole role)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var user = AddUser(dbContext, "rdr");
        await dbContext.SaveChangesAsync();
        return new GeneralUser { UserId = user.UserId, Username = user.Username, Role = role };
    }

    private sealed record SeededGames(
        GeneralUser Master,
        GeneralUser ForeignPlayer,
        string AwaitingEditsPublicId,
        string AwaitingApprovalPublicId,
        string PrivateDraftPublicId,
        string RemovedPublicId);

    /// <summary>
    /// Four games of one master, differing only in what hides them, plus a player
    /// of an unrelated game. Fresh identifiers every time: the fixture database is
    /// shared and its usernames and public ids are uniquely indexed.
    /// </summary>
    private async Task<SeededGames> SeedAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var master = AddUser(dbContext, "pmm");
        var foreignPlayer = AddUser(dbContext, "pmp");

        // MentorId is deliberately left null on both premoderated games: that is
        // the state a newbie's game is created in, and the whole point of the rank
        // arm is that no curator is recorded to admit anybody.
        var awaitingEdits = AddGame(dbContext, master.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingEdits);
        var awaitingApproval = AddGame(dbContext, master.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingApproval);
        var privateDraft = AddGame(dbContext, master.UserId,
            ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private);
        var removed = AddGame(dbContext, master.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingEdits);
        removed.IsRemoved = true;

        // The game the foreign player plays in, so that "a player" is a fact about
        // a real game and not an empty label on a stranger.
        var otherGame = AddGame(dbContext, master.UserId,
            ModuleStatus.Active, PremoderationStatus.Approved);
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = Guid.NewGuid(),
            GameId = otherGame.GameId,
            AuthorId = foreignPlayer.UserId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        return new SeededGames(
            new GeneralUser
            {
                UserId = master.UserId,
                Username = master.Username,
                Role = UserRole.RegularUser
            },
            new GeneralUser
            {
                UserId = foreignPlayer.UserId,
                Username = foreignPlayer.Username,
                Role = UserRole.RegularUser
            },
            awaitingEdits.PublicId!,
            awaitingApproval.PublicId!,
            privateDraft.PublicId!,
            removed.PublicId!);
    }

    private static DbUser AddUser(DmDbContext dbContext, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@premoderation.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static DbGame AddGame(
        DmDbContext dbContext,
        Guid masterId,
        ModuleStatus status,
        PremoderationStatus premoderationStatus,
        DraftVisibility draftVisibility = DraftVisibility.Public)
    {
        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = "Premoderation visibility game",
            Status = status,
            PremoderationStatus = premoderationStatus,
            DraftVisibility = draftVisibility,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Games.Add(game);
        return game;
    }
}
