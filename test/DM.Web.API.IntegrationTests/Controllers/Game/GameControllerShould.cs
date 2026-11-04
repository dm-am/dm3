using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Integration tests for GameController on one game. The list of games is in GameListShould.
/// </summary>
public class GameControllerShould : IntegrationTestBase
{
    public GameControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetGameWithValidIdReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"id\""); // Resource returned directly without wrapper
    }

    /// <summary>
    /// Get non-existent game should return NotFound, as for a game this reader may not see.
    /// </summary>
    [Fact]
    public async Task GetGameWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/games/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGameDetailsWithValidIdReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// GetGameDetails returns the game's active characters in `activeCharacters`, which the game and room tooltips read.
    /// </summary>
    [Fact]
    public async Task GetGameDetailsIncludesActiveCharactersWithOwners()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        // Seeded player characters must appear in the envelope's
        // activeCharacters array with their owner usernames.
        json.Should().Contain("activeCharacters");
        json.Should().Contain(TestConstants.TestCharacterName);
        json.Should().Contain(TestConstants.SecondCharacterName);
        json.Should().Contain(TestConstants.TestUserLogin);
        json.Should().Contain(TestConstants.SecondUserLogin);
    }

    /// <summary>
    /// GetGameDetails carries the recruitment PcCount the tooltip shows as "Персонажи: N/∞".
    /// </summary>
    [Fact]
    public async Task GetGameDetailsCarriesRealPcCount()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"pcCount\":2");
    }

    /// <summary>
    /// One endpoint, two ways to address the game, one payload.
    /// </summary>
    /// <remarks>The alias and the GUID take different repository paths; asserted as an equality of the two responses.</remarks>
    [Fact]
    public async Task GetGameAnswerTheSameWhetherAddressedByAliasOrByGuid()
    {
        var byGuid = await ReadGameAsync($"/v1/games/{TestConstants.TestGameId}");
        var byAlias = await ReadGameAsync($"/v1/games/{TestConstants.TestGamePublicId}");

        byAlias.GetProperty("id").GetString()
            .Should().Be(byGuid.GetProperty("id").GetString(),
                "the two addresses must resolve to the same game");

        foreach (var field in new[] { "subscribersCount", "gameReviewsCount", "postReviewsCount" })
        {
            byAlias.GetProperty(field).GetInt32()
                .Should().Be(byGuid.GetProperty(field).GetInt32(), field);
        }

        byAlias.GetProperty("activeCharacters").GetArrayLength()
            .Should().Be(byGuid.GetProperty("activeCharacters").GetArrayLength());
        byAlias.GetProperty("recruitment").GetProperty("pcCount").GetInt32()
            .Should().Be(byGuid.GetProperty("recruitment").GetProperty("pcCount").GetInt32());
    }

    /// <summary>The `resource` of a single-game read, as a JSON element.</summary>
    private async Task<JsonElement> ReadGameAsync(string url)
    {
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);

        // Parsed into a document that outlives the using: cloning is what makes
        // the element safe to read after the reader is disposed.
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("resource").Clone();
    }

    [Fact]
    public async Task GetGameDetailsWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/details");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
