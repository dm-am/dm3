using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Xunit;
using DM.Web.API.IntegrationTests.Helpers;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A write to a game answers with the details of the game, sheet included, the
/// way the details read answers, end to end.
/// </summary>
/// <remarks>
/// The repository hands back the row it wrote without the character sheet, and
/// every write answers through the details mapping, which requires one: a write
/// that skips filling the details in is stored and still answers with a server
/// error. So every write that answers with the game is asked here: the settings,
/// a move of the status, a premoderation verdict and the reset of the
/// recruitment date.
/// </remarks>
public class GameWriteAnswerShould : IntegrationTestBase
{
    public GameWriteAnswerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task AnswerTheSettingsSaveWithTheGame()
    {
        var (master, gameId) = await SeedAsync();
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/games/{gameId}/details", master);
        request.Content = JsonContent.Create(new { title = "Игра после правки" });

        var answer = await AnswerAsync(request);

        answer.GetProperty("title").GetString().Should().Be("Игра после правки");
        answer.TryGetProperty("schema", out _).Should().BeTrue("the details carry the character sheet");
    }

    [Fact]
    public async Task AnswerTheStartOfTheGameWithTheGame()
    {
        var (master, gameId) = await SeedAsync();
        var request = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/games/{gameId}/status", master);
        request.Content = JsonContent.Create(new { transition = "Start" });

        var answer = await AnswerAsync(request);

        answer.GetProperty("status").GetString().Should().Be(nameof(ModuleStatus.Active));
        answer.TryGetProperty("schema", out _).Should().BeTrue("the details carry the character sheet");
    }

    [Fact]
    public async Task AnswerThePremoderationVerdictWithTheGame()
    {
        var (_, gameId) = await SeedAsync(PremoderationStatus.AwaitingApproval);
        var request = CreateAdminRequest(HttpMethod.Post, $"/v1/games/{gameId}/premoderation");
        request.Content = JsonContent.Create(new { transition = "SetApproved" });

        var answer = await AnswerAsync(request);

        answer.GetProperty("premoderationStatus").GetString().Should().Be(nameof(PremoderationStatus.Approved));
        answer.TryGetProperty("schema", out _).Should().BeTrue("the details carry the character sheet");
    }

    [Fact]
    public async Task AnswerTheResetOfTheRecruitmentDateWithTheGame()
    {
        var (_, gameId) = await SeedAsync();
        var request = CreateAdminRequest(HttpMethod.Post, $"/v1/games/{gameId}/reset-recruitment-date");

        var answer = await AnswerAsync(request);

        answer.GetProperty("id").GetGuid().Should().Be(gameId);
        answer.TryGetProperty("schema", out _).Should().BeTrue("the details carry the character sheet");
    }

    private async Task<JsonElement> AnswerAsync(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resource").Clone();
    }

    /// <summary>
    /// A draft of a fresh master, open to everybody unless it waits for a verdict.
    /// Fresh identifiers every time: the fixture database is shared and its
    /// usernames and public ids are uniquely indexed.
    /// </summary>
    private async Task<(GeneralUser Master, Guid GameId)> SeedAsync(
        PremoderationStatus premoderation = PremoderationStatus.Approved)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"gwa{userId:N}"[..20],
            Email = $"{userId:N}@gamewriteanswer.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = userId,
            Title = "Игра до правки",
            Info = "Описание",
            Status = ModuleStatus.Draft,
            PremoderationStatus = premoderation,
            DraftVisibility = DraftVisibility.Public,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        db.Games.Add(game);
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(game.GameId));
        await db.SaveChangesAsync();
        return (new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser }, game.GameId);
    }
}
