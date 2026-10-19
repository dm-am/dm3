using System.Net;
using System.Text.Json;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

public class UserControllerShould : IntegrationTestBase
{
    public UserControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUsersReturnsOkWithList()
    {
        var response = await Client.GetAsync("/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// The per-category subscriber counts reach the wire.
    /// </summary>
    /// <remarks>The counts are UserSubscriberSummaryShould's; a missing object would read as three zeroes.</remarks>
    [Fact]
    public async Task GetUserCarriesTheSubscriberCountsPerCategory()
    {
        var response = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var counts = doc.RootElement
            .GetProperty("resource")
            .GetProperty("subscribersByCategory");

        counts.TryGetProperty("games", out _).Should().BeTrue();
        counts.TryGetProperty("blogs", out _).Should().BeTrue();
        counts.TryGetProperty("topics", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/v1/users/nonexistentuser123")]
    [InlineData("/v1/users/nonexistentuser123/profile")]
    public async Task AnswerNotFoundForAUserThatDoesNotExist(string address)
    {
        var response = await Client.GetAsync(address);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Filtering the roster by role is the paged listing, and only that.
    /// </summary>
    /// <remarks>API_DESIGN.md gives GET /v1/users?role=moderator for this and forbids a specialised address.</remarks>
    [Fact]
    public async Task FilterTheRosterByRoleThroughThePagedListing()
    {
        var listing = await Client.GetAsync("/v1/users?role=Admin&size=20");
        listing.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await listing.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
        content.Should().Contain("paging");

        var gone = await Client.GetAsync("/v1/users/by-role/Admin");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Users named by a prefix unique to this instance: the tier shares one database.
    /// </summary>
    private readonly string Prefix = $"ucs{Guid.NewGuid():N}"[..10];

    private async Task<string[]> AddUsersAsync(int count)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            var userId = Guid.NewGuid();
            // Username is varchar(20) and uniquely indexed, so the prefix carries
            // the identity and the tail only keeps the names apart.
            names[i] = $"{Prefix}{userId:N}"[..20];
            dbContext.Users.Add(new DM.Infrastructure.Persistence.Entities.Account.User
            {
                UserId = userId,
                Username = names[i],
                Email = $"{userId:N}@example.com",
                PasswordHash = "hash",
                Salt = "salt",
            });
        }

        await dbContext.SaveChangesAsync();
        return names;
    }

    /// <summary>Usernames of a /v1/users page, in the order the endpoint returned them.</summary>
    private async Task<string[]> UsernamesAsync(string query)
    {
        var response = await Client.GetAsync($"/v1/users?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => r.GetProperty("username").GetString()!)
            .ToArray();
    }

    /// <summary>
    /// The search filter narrows the list to the users it names.
    /// </summary>
    /// <remarks>An unknown query parameter is ignored, so what each parameter does to the answer is asserted.</remarks>
    [Fact]
    public async Task GetUsersWithSearchNarrowsTheListToTheMatches()
    {
        var mine = await AddUsersAsync(3);

        var matching = await UsernamesAsync($"search={mine[0]}&activity=All&take=100");

        // The filter is fuzzy on purpose: a name that contains the term OR is
        // close enough to it by trigram similarity. So the claim is not "every
        // row contains the term" — the two siblings seeded beside this one share
        // ten of twenty characters and are legitimately close.
        matching.Should().Contain(mine[0],
            "the term is a username spelled in full, and it has to find its own user");
        matching.Should().NotContain(TestConstants.TestUserLogin,
            "a name with nothing in common with the term is what an ignored parameter " +
            "would still hand back");
    }

    /// <summary>
    /// Every sort the list publishes is bound, and an unknown one is refused rather
    /// than silently answered in the default order.
    /// </summary>
    [Theory]
    [InlineData("Name")]
    [InlineData("Rating")]
    [InlineData("LastActivity")]
    [InlineData("Registered")]
    public async Task GetUsersWithSortOptionBindsTheSortAndRefusesAnUnknownOne(string sortBy)
    {
        var response = await Client.GetAsync($"/v1/users?sortBy={sortBy}&activity=All");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var unknown = await Client.GetAsync($"/v1/users?sortBy={sortBy}Nonsense&activity=All");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a sort field the list does not have is a validation error (API_DESIGN.md) — and a " +
            "parameter that refuses a wrong value is a parameter the binder actually reads");
    }

    /// <summary>
    /// Name ascending and name descending are each other's reverse, which is the
    /// cheapest proof that the pair of sort parameters reaches the query.
    /// </summary>
    [Fact]
    public async Task GetUsersWithSortOrderReversesTheList()
    {
        // Scoped to this test's own users and sorted by registration: a search sorts by relevance first.
        await AddUsersAsync(4);
        var scope = $"search={Prefix}&activity=All&take=100";

        var ascending = await UsernamesAsync($"sortBy=Registered&sortOrder=asc&{scope}");
        var descending = await UsernamesAsync($"sortBy=Registered&sortOrder=desc&{scope}");
        var again = await UsernamesAsync($"sortBy=Registered&sortOrder=asc&{scope}");

        ascending.Should().NotBeEmpty();

        // ORDER BY runs under the server's collation, so the order is stable and desc is asc reversed.
        again.Should().Equal(ascending,
            "the same request twice must answer in the same order, or paging over " +
            "this list drops and repeats rows between pages");
        descending.Should().Equal(ascending.Reverse(),
            "sortOrder is the direction of one ordering, not a second ordering");
    }

    /// <summary>
    /// The activity filter is named <c>activity</c>; <c>filter</c> was never its name
    /// and the endpoint answered 200 to it all the same.
    /// </summary>
    [Fact]
    public async Task GetUsersWithActivityFilterAdmitsMoreUsersThanTheDefault()
    {
        // Seeded with no LastActivityUtc, so All is the only filter that admits
        // them. Counted within this test's own prefix: the tier shares one
        // database, and a count over the whole registry measures whatever the
        // neighbouring classes happened to leave behind.
        var mine = await AddUsersAsync(2);
        var term = Prefix;

        var active = await UsernamesAsync($"search={term}&activity=Active&take=100");
        var all = await UsernamesAsync($"search={term}&activity=All&take=100");

        all.Should().Contain(mine);
        active.Should().NotContain(mine,
            "a user who has never been seen is not active, whatever the default filter " +
            "of the list happens to be");
        all.Length.Should().BeGreaterThan(active.Length);
    }

}
