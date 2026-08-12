using System.Net;
using System.Text.Json;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for UserController
/// </summary>
public class UserControllerShould : IntegrationTestBase
{
    public UserControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get users should return OK with list
    /// </summary>
    [Fact]
    public async Task GetUsers_ReturnsOkWithList()
    {
        // Act
        var response = await Client.GetAsync("/v1/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// The per-category subscriber counts reach the wire.
    /// </summary>
    /// <remarks>
    /// The counts themselves are asserted against the database in
    /// UserSubscriberSummaryShould; what this adds is that they survive the map
    /// to the API DTO and the serializer, because the profile line reads them and
    /// a missing object would silently read as three zeroes — which is the exact
    /// wrong answer the counts were added to stop.
    /// </remarks>
    [Fact]
    public async Task GetUser_CarriesTheSubscriberCountsPerCategory()
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

    /// <summary>
    /// Get user by login should return 404 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserByLogin_WithNonExistentUser_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/nonexistentuser123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Get user profile by login should return 404 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserProfile_WithNonExistentUser_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/nonexistentuser123/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Filtering the roster by role is the paged listing, and only that.
    /// </summary>
    /// <remarks>
    /// GET /v1/users/by-role/{role} answered the same question anonymously, with
    /// no page and no upper bound, over a result cached for an hour.
    /// API_DESIGN.md gives GET /v1/users?role=moderator as the shape for this and
    /// forbids the specialised address beside it, so the address is gone rather
    /// than paged: the second one only ever meant one endpoint could drift from
    /// the other.
    /// </remarks>
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

    #region Search Tests

    /// <summary>
    /// Users nobody else in the tier can see, named by a prefix unique to this
    /// instance. The tier shares one database and every class adds to it, so a
    /// filter asserted over the whole registry asserts what the neighbours left.
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
    /// <remarks>
    /// These tests asked for <c>?q=</c> and <c>?sort=</c> and asserted a 200. Both
    /// names were retired — the list takes <c>search</c> and <c>sortBy</c> — and a
    /// query parameter the binder does not know is ignored rather than refused, so
    /// every one of them went on passing against the default unsorted list. A 200 is
    /// what the unfiltered answer looks like, which is why what each parameter did to
    /// the answer is asserted here instead.
    /// </remarks>
    [Fact]
    public async Task GetUsers_WithSearch_NarrowsTheListToTheMatches()
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
    public async Task GetUsers_WithSortOption_BindsTheSortAndRefusesAnUnknownOne(string sortBy)
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
    public async Task GetUsers_WithSortOrder_ReversesTheList()
    {
        // Scoped to this test's own users. take=100 over the shared registry cut
        // the list once the tier grew past a hundred rows, and a truncated
        // descending page is the LAST hundred reversed, not the first hundred.
        //
        // Sorted by registration and not by name: with a search term the name
        // sort answers by relevance first, which is what a search is for, so
        // reversing the direction reorders only the ties. Registration is a plain
        // ordering, and the reversal is exact.
        await AddUsersAsync(4);
        var scope = $"search={Prefix}&activity=All&take=100";

        var ascending = await UsernamesAsync($"sortBy=Registered&sortOrder=asc&{scope}");
        var descending = await UsernamesAsync($"sortBy=Registered&sortOrder=desc&{scope}");
        var again = await UsernamesAsync($"sortBy=Registered&sortOrder=asc&{scope}");

        ascending.Should().NotBeEmpty();

        // Not compared against a .NET comparer. ORDER BY runs under the server's
        // collation, and this suite's container is postgres:16-alpine, which has
        // no locales and falls back to C — byte order, where every capital sorts
        // before every lower-case letter and Cyrillic after all of Latin. The
        // stand runs en_US.utf8 and orders the same names differently, so an
        // absolute expectation here would assert the locale of the test image.
        // The divergence itself is a finding of its own (username-order-depends-on-locale).
        //
        // What the endpoint does promise is asserted instead: the order is
        // stable, and desc is exactly asc reversed.
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
    public async Task GetUsers_WithActivityFilter_AdmitsMoreUsersThanTheDefault()
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

    #endregion
}
