using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Who may replace the character sheet of a game, and in what order the
/// endpoint refuses everybody else.
/// </summary>
/// <remarks>
/// The sheet belongs to the game, so the gate is the game's: the master writes
/// it, a player does not, a stranger is not told the game exists, and a caller
/// with no session is asked to sign in before any of that is decided.
/// </remarks>
public class GameAttributeSchemaEndpointShould : IntegrationTestBase
{
    public GameAttributeSchemaEndpointShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseAnAnonymousCaller()
    {
        var gameId = await SeedGame(TestConstants.TestUserId);

        var response = await Client.PutAsJsonAsync(Url(gameId), Body(0));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseACallerWhoDoesNotLeadTheGame()
    {
        var gameId = await SeedGame(TestConstants.TestUserId);

        var request = CreateAuthenticatedRequest(
            HttpMethod.Put, Url(gameId), CustomWebApplicationFactory.CreateSecondUser());
        request.Content = JsonContent.Create(Body(0));
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefuseAGameThatIsNotThere()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Put, Url(Guid.NewGuid()));
        request.Content = JsonContent.Create(Body(0));
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A caller with no version never read the sheet they are replacing, and
    /// this write deletes the values of every specification it drops.
    /// </summary>
    [Fact]
    public async Task RefuseABodyWithoutAVersion()
    {
        var gameId = await SeedGame(TestConstants.TestUserId);

        var request = CreateAuthenticatedRequest(HttpMethod.Put, Url(gameId));
        request.Content = JsonContent.Create(new { specifications = Array.Empty<object>() });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The master writes the sheet, gets the stored set back with a new
    /// version, and the details of the game carry the same pair afterwards.
    /// </summary>
    [Fact]
    public async Task ReplaceTheSheetForTheMasterAndCarryItIntoTheDetails()
    {
        var gameId = await SeedGame(TestConstants.TestUserId);

        var request = CreateAuthenticatedRequest(HttpMethod.Put, Url(gameId));
        request.Content = JsonContent.Create(Body(0));
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var written = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var writtenSheet = written.RootElement.GetProperty("resource");
        writtenSheet.GetProperty("version").GetInt32().Should().Be(1);
        writtenSheet.GetProperty("specifications").GetArrayLength().Should().Be(1);

        var details = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/games/{gameId}/details"));
        details.StatusCode.Should().Be(HttpStatusCode.OK);
        using var read = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        var readSheet = read.RootElement.GetProperty("resource").GetProperty("schema");
        readSheet.GetProperty("version").GetInt32().Should().Be(1);
        readSheet.GetProperty("specifications")[0].GetProperty("title").GetString().Should().Be("Раса");
    }

    /// <summary>
    /// The second of two saves made from the same reading is refused, and the
    /// reader is told to look again rather than left believing they won.
    /// </summary>
    [Fact]
    public async Task RefuseASecondReplacementMadeFromTheSameReading()
    {
        var gameId = await SeedGame(TestConstants.TestUserId);

        var first = CreateAuthenticatedRequest(HttpMethod.Put, Url(gameId));
        first.Content = JsonContent.Create(Body(0));
        (await Client.SendAsync(first)).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = CreateAuthenticatedRequest(HttpMethod.Put, Url(gameId));
        second.Content = JsonContent.Create(Body(0));
        var response = await Client.SendAsync(second);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static string Url(Guid gameId) => $"/v1/games/{gameId}/attribute-schema";

    private static object Body(int version) => new
    {
        version,
        specifications = new[]
        {
            new { title = "Раса", type = (int)AttributeSpecificationType.Text, order = 0 }
        }
    };

    /// <summary>
    /// A fresh game of the given master, with an empty sheet at version zero.
    /// </summary>
    private async Task<Guid> SeedGame(Guid masterId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var gameId = Guid.NewGuid();
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = "Игра для проверки анкеты",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        dbContext.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(gameId));
        await dbContext.SaveChangesAsync();
        return gameId;
    }
}
