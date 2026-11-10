using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class PostControllerShould : IntegrationTestBase
{
    public PostControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // Asserted by JSON traversal: a stray `"master":` anywhere would pass a substring match.
    [Fact]
    public async Task GetRatedPostsReturnsFullGameRefOnPostRoomGame()
    {
        // Scoped to the seeded game. Unscoped, this took the first row of a
        // global rating-ordered list and assumed it was the seed's — true only
        // while nothing else in the database had a rated post, which is not a
        // property of the endpoint under test.
        var response = await Client.GetAsync($"/v1/posts?gameId={TestConstants.TestGameId}&take=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var resources = doc.RootElement.GetProperty("resources");
        resources.GetArrayLength().Should().BeGreaterThan(0,
            "the seed includes one reviewed post in TestGame");

        var post = resources.EnumerateArray().First();
        var room = post.GetProperty("room");
        room.TryGetProperty("game", out var game).Should().BeTrue(
            "post.room.game is what GameLink / RoomLink read for tooltips");

        // Tooltip fields the sidebar primitive buildTooltip() reads.
        // These MUST be present and non-null on post.room.game, otherwise
        // the game tooltip on featured posts drifts from the sidebar one.
        game.TryGetProperty("master", out var master).Should().BeTrue();
        master.ValueKind.Should().Be(JsonValueKind.Object);
        master.GetProperty("username").GetString()
            .Should().Be(TestConstants.TestUserLogin);

        game.TryGetProperty("assistants", out _).Should().BeTrue();

        game.TryGetProperty("recruitment", out var recruitment).Should().BeTrue();
        recruitment.GetProperty("pcCount").GetInt32()
            .Should().Be(2, "two player characters are seeded in TestGame");

        game.TryGetProperty("subscribersCount", out _).Should().BeTrue();

        // The room tooltip names every active character with its owner.
        game.TryGetProperty("activeCharacters", out var activeChars).Should().BeTrue();
        activeChars.ValueKind.Should().Be(JsonValueKind.Array);
        activeChars.GetArrayLength().Should().Be(2);
        var names = activeChars.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToArray();
        names.Should().Contain(TestConstants.TestCharacterName);
        names.Should().Contain(TestConstants.SecondCharacterName);
        var owners = activeChars.EnumerateArray()
            .Select(c => c.GetProperty("ownerUsername").GetString())
            .ToArray();
        owners.Should().Contain(TestConstants.TestUserLogin);
        owners.Should().Contain(TestConstants.SecondUserLogin);
    }

    // "Reader" is capitalised: the enum converter has no naming policy, and the client mirrors the
    // string.
    [Fact]
    public async Task GetRatedPostsReportsTheViewerAsAReaderOfTheGameTheySubscribeTo()
    {
        // No arrange: the fixture already subscribes InactiveUser1 to TestGame.
        var subscriber = new GeneralUser
        {
            UserId = TestConstants.InactiveUser1Id,
            Username = TestConstants.InactiveUser1Username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
        };

        var participation = await ReadRatedPostParticipationAsync(
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/posts?gameId={TestConstants.TestGameId}&take=10",
                subscriber));

        participation.Should().Contain("Reader");
    }

    // Without it the assertion above passes on a resolver that returns every bit for everyone.
    [Fact]
    public async Task GetRatedPostsDoesNotReportAStrangerAsAReader()
    {
        var stranger = new GeneralUser
        {
            UserId = TestConstants.InactiveUser2Id,
            Username = TestConstants.InactiveUser2Username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
        };

        var participation = await ReadRatedPostParticipationAsync(
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/posts?gameId={TestConstants.TestGameId}&take=10",
                stranger));

        // InactiveUser2 subscribes to SecondGame, not to this one — so the read
        // also shows the flag is filled per game and not per subscriber.
        participation.Should().NotContain("Reader");
    }

    private async Task<string[]> ReadRatedPostParticipationAsync(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resources = doc.RootElement.GetProperty("resources");
        resources.GetArrayLength().Should().BeGreaterThan(0,
            "the seed includes one reviewed post in TestGame");

        var game = resources.EnumerateArray().First()
            .GetProperty("room")
            .GetProperty("game");

        return game.GetProperty("participation")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }
}
