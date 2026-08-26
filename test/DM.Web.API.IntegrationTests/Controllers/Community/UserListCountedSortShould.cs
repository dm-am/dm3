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
/// Games hosted, games played, blogs hosted and active subscribers are not columns
/// of Users. They used to be counted by a subquery correlated to the user row inside
/// the ORDER BY, which Postgres evaluates once per registration and before OFFSET;
/// they are now one aggregate per counted table, left-joined to the page.
///
/// Two independent computations meet here, which is what makes the assertion worth
/// making: the order comes from the join in SQL, and the number each row carries is
/// produced afterwards by a separate batched GROUP BY over the same tables. A counter
/// wired to the wrong table orders the list against the numbers it shows, and a join
/// that stopped being a left one silently drops everyone whose count is zero.
/// </remarks>
public class UserListCountedSortShould : IntegrationTestBase
{
    public UserListCountedSortShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private async Task<(string Username, int Count)[]> ListAsync(string field, string order)
    {
        var response = await Client.GetAsync(
            $"/v1/users?sortBy={field}&sortOrder={order}&activity=All&take=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var property = char.ToLowerInvariant(field[0]) + field[1..];
        return doc.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => (
                Username: r.GetProperty("username").GetString()!,
                Count: r.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32()
                    : 0))
            .ToArray();
    }

    [Theory]
    [InlineData("GamesHosting")]
    [InlineData("BlogsHosting")]
    [InlineData("GamesPlaying")]
    public async Task OrderTheListByTheNumberTheRowsShow(string field)
    {
        var descending = await ListAsync(field, "desc");
        var ascending = await ListAsync(field, "asc");

        descending.Select(x => x.Count).Should().BeInDescendingOrder(
            "the sort key is the counter, and the row carries the counter it was sorted by");
        ascending.Select(x => x.Count).Should().BeInAscendingOrder(
            "asking for the other direction reverses the number, not the meaning of it");
    }

    /// <summary>
    /// Counted by page size rather than by membership: the registry the suite leaves
    /// behind is longer than one page, so two pages of it hold different people, and
    /// the question here is how many the sort admits, not which.
    /// </summary>
    [Theory]
    [InlineData("GamesHosting")]
    [InlineData("BlogsHosting")]
    [InlineData("GamesPlaying")]
    [InlineData("Popularity")]
    public async Task KeepEveryUserTheAlphabeticalListHas(string field)
    {
        var byName = await ListAsync("Name", "asc");
        var byCount = await ListAsync(field, "desc");

        byCount.Should().HaveCount(byName.Length,
            "the counter is left-joined: a user with none of the counted thing counts zero, " +
            "which is what the correlated Count() answered for him, and he stays on the list");
        byCount[^1].Count.Should().Be(0,
            "most of the registry hosts and plays nothing, so the far end of the descending " +
            "page is theirs — an inner join would fill the page with the few who have rows");
    }

    /// <summary>
    /// The counter is the one that belongs to the sort: the seeded master is on the
    /// page his hosting count entitles him to, and that page is short.
    /// </summary>
    /// <remarks>
    /// Position is the whole claim, so the page has to be short enough for position to
    /// mean something. This test used to ask for a hundred rows, which is longer than
    /// the registry the suite leaves behind — and on a page that holds everybody,
    /// "he is on it" and "he hosts three games" are true whatever the sort was wired
    /// to. It stayed green under the very defect it is named for.
    ///
    /// So the window is computed rather than guessed: read the whole list once to learn
    /// how many users out-host him and how many tie with him, then ask for exactly that
    /// many rows. A correct descending sort must place him inside that window; a sort
    /// wired to the neighbouring counter puts him where his games PLAYED puts him, and
    /// he plays none.
    /// </remarks>
    [Fact]
    public async Task SortByTheHostingCounterAndNotByANeighbouringOne()
    {
        var whole = await ListAsync("GamesHosting", "desc");
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
