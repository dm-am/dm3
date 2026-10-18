using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Every order the game list offers is the order it promises, in both
/// directions.
/// </summary>
/// <remarks>
/// The ordering is one method of nine branches, each a different expression over
/// up to four columns, and a request that asks for an order only proves the list
/// came back. A branch moved with one column swapped for its neighbour, or with
/// a direction turned, answers 200 all the same; only the sequence says it.
///
/// Against the container Postgres because the order is the database's: booleans
/// sort false first there, the fallbacks for a missing date are coalesced in SQL,
/// and relevance is a trigram similarity no other provider computes.
///
/// One set of nine games, built so that no two orders below are the same list:
/// a branch answering with its neighbour's expression is a different sequence of
/// titles, not a coincidence that passes.
/// </remarks>
public class GameListSortingShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The nine games: title, status, recruitment open, recruitment count, closed
    /// reason, and the days after <see cref="Start"/> they were created,
    /// activated, closed and opened recruitment, with their popularity.
    /// </summary>
    private static readonly IReadOnlyList<Seed> Forest =
    [
        new("Alder", ModuleStatus.Draft, false, 0, ClosedReason.None, 5, null, null, null, 50),
        new("Birch", ModuleStatus.Active, true, 1, ClosedReason.None, 3, 12, null, 22, 10),
        new("Cedar", ModuleStatus.Active, true, 2, ClosedReason.None, 9, 13, null, 21, 70),
        new("Dogwood", ModuleStatus.Active, false, 1, ClosedReason.None, 1, 11, null, 24, 30),
        new("Elm", ModuleStatus.Closed, false, 1, ClosedReason.Frozen, 7, 14, 31, null, 90),
        new("Fir", ModuleStatus.Closed, false, 1, ClosedReason.Finished, 2, 15, 33, 25, 20),
        new("Ginkgo", ModuleStatus.Closed, false, 0, ClosedReason.None, 8, null, 32, null, 60),
        new("Hazel", ModuleStatus.Draft, false, 0, ClosedReason.None, 4, null, null, 20, 40),
        new("Ivy", ModuleStatus.Active, true, 1, ClosedReason.None, 6, 16, null, 23, 80),
    ];

    public GameListSortingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    public static TheoryData<string, string, string[]> Keys => new()
    {
        { "recruitmentstarted", "asc", ["Hazel", "Cedar", "Birch", "Ivy", "Dogwood", "Fir", "Alder", "Elm", "Ginkgo"] },
        { "recruitmentstarted", "desc", ["Fir", "Dogwood", "Ivy", "Birch", "Cedar", "Hazel", "Alder", "Elm", "Ginkgo"] },
        { "title", "asc", ["Alder", "Birch", "Cedar", "Dogwood", "Elm", "Fir", "Ginkgo", "Hazel", "Ivy"] },
        { "title", "desc", ["Ivy", "Hazel", "Ginkgo", "Fir", "Elm", "Dogwood", "Cedar", "Birch", "Alder"] },
        { "created", "asc", ["Dogwood", "Fir", "Birch", "Hazel", "Alder", "Ivy", "Elm", "Ginkgo", "Cedar"] },
        { "created", "desc", ["Cedar", "Ginkgo", "Elm", "Ivy", "Alder", "Hazel", "Birch", "Fir", "Dogwood"] },
        // Games never activated go last whichever way the dates run.
        { "activated", "asc", ["Dogwood", "Birch", "Cedar", "Elm", "Fir", "Ivy", "Alder", "Ginkgo", "Hazel"] },
        { "activated", "desc", ["Ivy", "Fir", "Elm", "Cedar", "Birch", "Dogwood", "Alder", "Ginkgo", "Hazel"] },
        // Games never closed go last whichever way the dates run.
        { "closed", "asc", ["Elm", "Ginkgo", "Fir", "Alder", "Birch", "Cedar", "Dogwood", "Hazel", "Ivy"] },
        { "closed", "desc", ["Fir", "Ginkgo", "Elm", "Alder", "Birch", "Cedar", "Dogwood", "Hazel", "Ivy"] },
        // Active, then closed, then drafts going down; the reverse going up.
        { "popularity", "asc", ["Hazel", "Alder", "Fir", "Ginkgo", "Elm", "Birch", "Dogwood", "Cedar", "Ivy"] },
        { "popularity", "desc", ["Ivy", "Cedar", "Dogwood", "Birch", "Elm", "Ginkgo", "Fir", "Alder", "Hazel"] },
        // Seven groups by status and recruitment, the latest date first inside each.
        { "status", "asc", ["Alder", "Hazel", "Ivy", "Birch", "Cedar", "Dogwood", "Elm", "Fir", "Ginkgo"] },
        { "status", "desc", ["Ginkgo", "Fir", "Elm", "Dogwood", "Cedar", "Ivy", "Birch", "Alder", "Hazel"] },
        // The recruiting games lead in both directions; only the date inside a group turns.
        { "availableslots", "asc", ["Cedar", "Birch", "Ivy", "Hazel", "Alder", "Dogwood", "Elm", "Fir", "Ginkgo"] },
        { "availableslots", "desc", ["Ivy", "Birch", "Cedar", "Hazel", "Alder", "Dogwood", "Elm", "Fir", "Ginkgo"] },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task OrderTheListByEveryKeyInBothDirections(string sortBy, string sortOrder, string[] expected)
    {
        var owner = await SeedAsync(Forest);

        var titles = await ListAsync(owner, new GamesQuery { SortBy = sortBy, SortOrder = sortOrder });

        titles.Should().Equal(expected, $"sortBy={sortBy}&sortOrder={sortOrder} is the order the list promises");
    }

    /// <summary>
    /// With no key, the list runs by the date a game started, the date it was
    /// created standing in for a game never activated.
    /// </summary>
    [Theory]
    [InlineData("asc", new[] { "Hazel", "Alder", "Ginkgo", "Dogwood", "Birch", "Cedar", "Elm", "Fir", "Ivy" })]
    [InlineData("desc", new[] { "Ivy", "Fir", "Elm", "Cedar", "Birch", "Dogwood", "Ginkgo", "Alder", "Hazel" })]
    public async Task OrderTheListByTheStartOfEachGameWithoutAKey(string sortOrder, string[] expected)
    {
        var owner = await SeedAsync(Forest);

        var titles = await ListAsync(owner, new GamesQuery { SortOrder = sortOrder });

        titles.Should().Equal(expected);
    }

    /// <summary>
    /// A list of closed games alone runs by the day each was closed, which is not
    /// the order the same three games take in the list without a key.
    /// </summary>
    [Theory]
    [InlineData("asc", new[] { "Elm", "Ginkgo", "Fir" })]
    [InlineData("desc", new[] { "Fir", "Ginkgo", "Elm" })]
    public async Task OrderTheClosedGamesByTheDayTheyClosed(string sortOrder, string[] expected)
    {
        var owner = await SeedAsync(Forest);

        var titles = await ListAsync(owner, new GamesQuery
        {
            Statuses = [ModuleStatus.Closed],
            SortOrder = sortOrder,
        });

        titles.Should().Equal(expected);
    }

    /// <summary>
    /// A search without a key answers the closest titles first: the exact title,
    /// then a title that starts with the words, then the rest by trigram
    /// similarity rather than by the alphabet, which decides only between two
    /// titles equally close.
    /// </summary>
    [Fact]
    public async Task PutTheClosestTitlesFirstWhenASearchNamesNoKey()
    {
        var owner = await SeedAsync(Relevance);

        var titles = await ListAsync(owner, new GamesQuery { Search = "ember" });

        titles.Should().Equal(["Ember", "Embersong", "Yember", "Zember", "Amber Ember Lanterns"],
            "a title nothing like the words is not found at all, among those found the alphabet " +
            "would put the long one before the close one, and between two equally close the alphabet decides");
    }

    /// <summary>A search with a key answers in the order of the key.</summary>
    [Fact]
    public async Task OrderASearchByTheKeyItNames()
    {
        var owner = await SeedAsync(Relevance);

        var titles = await ListAsync(owner, new GamesQuery { Search = "ember", SortBy = "title", SortOrder = "asc" });

        titles.Should().Equal(["Amber Ember Lanterns", "Ember", "Embersong", "Yember", "Zember"]);
    }

    /// <summary>
    /// Five titles a search for "ember" finds in four different ways, two of them
    /// equally close so that the title decides between them, and one it does not
    /// find.
    /// </summary>
    private static readonly IReadOnlyList<Seed> Relevance =
    [
        new("Zember", ModuleStatus.Active, false, 1, ClosedReason.None, 1, 1, null, null, 0),
        new("Amber Ember Lanterns", ModuleStatus.Active, false, 1, ClosedReason.None, 2, 2, null, null, 0),
        new("Embersong", ModuleStatus.Active, false, 1, ClosedReason.None, 3, 3, null, null, 0),
        new("Ember", ModuleStatus.Active, false, 1, ClosedReason.None, 4, 4, null, null, 0),
        new("Birchwood", ModuleStatus.Active, false, 1, ClosedReason.None, 5, 5, null, null, 0),
        new("Yember", ModuleStatus.Active, false, 1, ClosedReason.None, 6, 6, null, null, 0),
    ];

    public static TheoryData<string?, string, bool, bool> TieBreaks => new()
    {
        { "recruitmentstarted", "asc", false, false },
        { "recruitmentstarted", "desc", false, false },
        { "created", "asc", false, false },
        { "created", "desc", false, false },
        { "activated", "asc", false, false },
        { "activated", "desc", false, false },
        { "closed", "asc", false, false },
        { "closed", "desc", false, false },
        // Popularity going up is popularity going down read backwards, titles included.
        { "popularity", "asc", false, true },
        { "popularity", "desc", false, false },
        { "status", "asc", false, false },
        { "status", "desc", false, false },
        { "availableslots", "asc", false, false },
        { "availableslots", "desc", false, false },
        { null, "asc", false, false },
        { null, "desc", false, false },
        { null, "asc", true, false },
        { null, "desc", true, false },
    };

    /// <summary>
    /// Two games equal in the key fall back to the title, in every order.
    /// </summary>
    /// <remarks>
    /// On the site equal keys are the common case rather than the edge: every new
    /// game has the same popularity, a draft has no recruitment date, and a list
    /// is read a page at a time. Without a fixed second key the database may
    /// answer tied games in any order on each request, and the next page repeats
    /// one game and skips another. The nine games above are all unequal, so the
    /// title after the key is checked here, on pairs of games that differ in
    /// nothing else.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TieBreaks))]
    public async Task BreakATieByTheTitle(string? sortBy, string sortOrder, bool closedOnly, bool titlesRunBackwards)
    {
        var owner = await SeedAsync(Twins);

        var titles = await ListAsync(owner, new GamesQuery
        {
            SortBy = sortBy,
            SortOrder = sortOrder,
            Statuses = closedOnly ? [ModuleStatus.Closed] : null,
        });

        var pairs = Twins.Chunk(2)
            .Where(pair => !closedOnly || pair[0].Status == ModuleStatus.Closed)
            .ToList();
        titles.Should().HaveCount(pairs.Count * 2);
        foreach (var pair in pairs)
        {
            var (before, after) = titlesRunBackwards ? (pair[1], pair[0]) : (pair[0], pair[1]);
            titles.Should().ContainInConsecutiveOrder([before.Title, after.Title],
                $"{before.Title} and {after.Title} share every key, and only the title can order them");
        }
    }

    /// <summary>
    /// Five pairs of games that differ only in the title, the two titles of a
    /// pair next to each other in the alphabet: wherever several pairs tie at
    /// once, each pair still stands together.
    /// </summary>
    private static readonly IReadOnlyList<Seed> Twins =
    [
        new("Larch", ModuleStatus.Draft, false, 0, ClosedReason.None, 3, null, null, null, 1),
        new("Linden", ModuleStatus.Draft, false, 0, ClosedReason.None, 3, null, null, null, 1),
        new("Maple", ModuleStatus.Active, true, 1, ClosedReason.None, 1, 10, null, 20, 4),
        new("Myrtle", ModuleStatus.Active, true, 1, ClosedReason.None, 1, 10, null, 20, 4),
        new("Oak", ModuleStatus.Active, false, 1, ClosedReason.None, 2, 11, null, 19, 6),
        new("Olive", ModuleStatus.Active, false, 1, ClosedReason.None, 2, 11, null, 19, 6),
        new("Pine", ModuleStatus.Closed, false, 1, ClosedReason.Frozen, 4, 12, 30, null, 2),
        new("Poplar", ModuleStatus.Closed, false, 1, ClosedReason.Frozen, 4, 12, 30, null, 2),
        new("Spruce", ModuleStatus.Closed, false, 1, ClosedReason.Finished, 5, 13, 31, null, 3),
        new("Sumac", ModuleStatus.Closed, false, 1, ClosedReason.Finished, 5, 13, 31, null, 3),
    ];

    private sealed record Seed(
        string Title,
        ModuleStatus Status,
        bool IsRecruitmentOpen,
        int RecruitmentCount,
        ClosedReason ClosedReason,
        int CreatedDay,
        int? ActivatedDay,
        int? ClosedDay,
        int? RecruitmentStartedDay,
        int PopularityScore);

    private sealed record Owner(Guid UserId, string Username);

    /// <summary>
    /// The games under a master of their own: the fixture's database is shared,
    /// and the owner filter keeps every other test's games out of the list.
    /// </summary>
    private async Task<Owner> SeedAsync(IEnumerable<Seed> seeds)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed.
        var username = $"sort{masterId:N}"[..20];
        dbContext.Users.Add(new DbUser
        {
            UserId = masterId,
            Username = username,
            Email = $"{masterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });

        foreach (var seed in seeds)
        {
            dbContext.Set<DbGame>().Add(new DbGame
            {
                GameId = Guid.NewGuid(),
                PublicId = $"s{Guid.NewGuid():N}",
                MasterId = masterId,
                Title = seed.Title,
                Status = seed.Status,
                IsRecruitmentOpen = seed.IsRecruitmentOpen,
                RecruitmentCount = seed.RecruitmentCount,
                ClosedReason = seed.ClosedReason,
                CreatedUtc = Day(seed.CreatedDay),
                ActivatedUtc = Day(seed.ActivatedDay),
                ClosedUtc = Day(seed.ClosedDay),
                RecruitmentStartedUtc = Day(seed.RecruitmentStartedDay),
                PopularityScore = seed.PopularityScore,
                PremoderationStatus = PremoderationStatus.Approved,
                CommentsAccessMode = CommentsAccessMode.Public,
            });
        }

        await dbContext.SaveChangesAsync();
        return new Owner(masterId, username);
    }

    /// <summary>The titles of the owner's games, in the order the list gives them.</summary>
    private async Task<IReadOnlyList<string>> ListAsync(Owner owner, GamesQuery query)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        query.OwnerUsernames = [owner.Username];
        query.Take = 50;

        var games = await repository.GetGames(new PagingData(query, 50, 50), query, owner.UserId);
        return games.Select(game => game.Title).ToList();
    }

    private static DateTimeOffset Day(int day) => Start.AddDays(day);

    private static DateTimeOffset? Day(int? day) => day is null ? null : Start.AddDays(day.Value);
}
