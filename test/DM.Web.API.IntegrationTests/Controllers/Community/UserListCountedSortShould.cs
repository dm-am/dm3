using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// The four sorts over a counted number order the list by that number, and drop
/// nobody for having none of it.
/// </summary>
/// <remarks>
/// The order comes from a left join in SQL and the number from a separate batched GROUP BY, so a counter wired
/// to the wrong table orders against the numbers it shows.
/// </remarks>
public class UserListCountedSortShould : IntegrationTestBase
{
    public UserListCountedSortShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private sealed record Page((string Username, int Count)[] Rows, int Total);

    private async Task<Page> ListAsync(string field, string order)
    {
        var response = await Client.GetAsync(
            $"/v1/users?sortBy={field}&sortOrder={order}&activity=All&take=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var property = char.ToLowerInvariant(field[0]) + field[1..];
        var rows = doc.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => (
                Username: r.GetProperty("username").GetString()!,
                Count: r.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32()
                    : 0))
            .ToArray();
        return new Page(rows, doc.RootElement.GetProperty("paging").GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("GamesHosting")]
    [InlineData("BlogsHosting")]
    [InlineData("GamesPlaying")]
    public async Task OrderTheListByTheNumberTheRowsShow(string field)
    {
        var descending = await ListAsync(field, "desc");
        var ascending = await ListAsync(field, "asc");

        descending.Rows.Select(x => x.Count).Should().BeInDescendingOrder(
            "the sort key is the counter, and the row carries the counter it was sorted by");
        ascending.Rows.Select(x => x.Count).Should().BeInAscendingOrder(
            "asking for the other direction reverses the number, not the meaning of it");
    }

    /// <summary>
    /// Counted by the reported total: the ascending list opens with the users an inner join would drop.
    /// </summary>
    [Theory]
    [InlineData("GamesHosting")]
    [InlineData("BlogsHosting")]
    [InlineData("GamesPlaying")]
    [InlineData("Popularity")]
    public async Task KeepEveryUserTheAlphabeticalListHas(string field)
    {
        var byName = await ListAsync("Name", "asc");
        var byCount = await ListAsync(field, "asc");

        byCount.Total.Should().Be(byName.Total,
            "the counter is left-joined: a user with none of the counted thing counts zero, " +
            "which is what the correlated Count() answered for him, and he stays on the list");
        byCount.Rows[0].Count.Should().Be(0,
            "most of the registry hosts and plays nothing, so the ascending list opens with " +
            "them — an inner join would open it with the few who have rows");
    }

    /// <summary>
    /// The counter is the one that belongs to the sort: the seeded master is on the
    /// page his hosting count entitles him to, and that page is short.
    /// </summary>
    /// <remarks>
    /// The window is computed from the whole list, so a sort wired to the neighbouring counter puts him outside
    /// the short page.
    /// </remarks>
    [Fact]
    public async Task SortByTheHostingCounterAndNotByANeighbouringOne()
    {
        var whole = (await ListAsync("GamesHosting", "desc")).Rows;
        whole.Should().NotBeEmpty();

        var seeded = whole.Should().ContainSingle(x => x.Username == TestConstants.TestUserLogin).Subject;
        seeded.Count.Should().BeGreaterThanOrEqualTo(3,
            "he masters three of the seeded games, and hosting counts mastering plus assisting");

        // Everyone who must precede him, plus everyone the username tie-break may put
        // ahead of him among his equals. He cannot fall outside that.
        var window = whole.Count(x => x.Count > seeded.Count) + whole.Count(x => x.Count == seeded.Count);
        window.Should().BeLessThan(whole.Length,
            "the window has to exclude somebody to be worth asserting: most of the registry " +
            "hosts nothing, so a page this size cannot hold everyone");

        var response = await Client.GetAsync(
            $"/v1/users?sortBy=GamesHosting&sortOrder=desc&activity=All&take={window}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var page = doc.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => r.GetProperty("username").GetString()!)
            .ToArray();

        page.Should().Contain(TestConstants.TestUserLogin,
            $"he hosts {seeded.Count} games, which puts him inside the first {window} rows of the " +
            "descending list — unless the sort is reading a counter that is not hosting");
    }
}
