using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class GameListShould : IntegrationTestBase
{
    public GameListShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>Every sort, search and filter of the list answers a guest.</summary>
    [Theory]
    [InlineData("/v1/games")]
    [InlineData("/v1/games?search=test")]
    [InlineData("/v1/games?search=test&sortBy=created")]
    [InlineData("/v1/games?search=test&size=5&number=1")]
    [InlineData("/v1/games?search=test&statuses=Active")]
    [InlineData("/v1/games?sortBy=popularity&sortOrder=desc")]
    [InlineData("/v1/games?sortBy=popularity&sortOrder=desc&take=10")]
    [InlineData("/v1/games?sortBy=popularity&sortOrder=asc")]
    [InlineData("/v1/games?sortBy=title&sortOrder=asc")]
    [InlineData("/v1/games?sortBy=status&sortOrder=asc")]
    [InlineData("/v1/games?sortBy=created&sortOrder=desc")]
    public async Task ListTheGamesForEveryQuery(string address)
    {
        var response = await Client.GetAsync(address);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        content.Should().Contain("resources");
    }

    [Fact]
    public async Task GetGamesWithPagingEchoesSkipAndTake()
    {
        var response = await Client.GetAsync("/v1/games?skip=0&take=10");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        var paging = JsonDocument.Parse(content).RootElement.GetProperty("paging");
        paging.GetProperty("skip").GetInt32().Should().Be(0);
        paging.GetProperty("take").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetOwnGamesWhenNotAuthenticatedReturnsEmptyList()
    {
        var response = await Client.GetAsync("/v1/games?participating=true");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        JsonDocument.Parse(content).RootElement
            .GetProperty("resources").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GetTagsWithNoParametersReturnsOk()
    {
        var response = await Client.GetAsync("/v1/games/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
