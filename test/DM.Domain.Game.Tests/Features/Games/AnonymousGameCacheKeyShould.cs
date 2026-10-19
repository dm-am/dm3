using System.Linq;
using System.Reflection;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

/// <summary>
/// One anonymous listing, one cache entry.
/// </summary>
/// <remarks>
/// Walked by reflection, so a new query field must end up in the key or in the shared list.
/// </remarks>
public class AnonymousGameCacheKeyShould
{
    [Fact]
    public void TellApartEveryQueryItAgreesToCache()
    {
        var properties = QueryProbes.WritableProperties(typeof(GamesQuery));

        properties.Should().NotBeEmpty(
            "the query declares the fields this is about, and finding none passes everything");

        var plain = new GamesQuery();
        GamesQueryCacheKey.IsCacheableAnonymousQuery(plain).Should().BeTrue(
            "the plainest listing there is - the front page of an anonymous reader - is the " +
            "one this cache exists for");
        var plainKey = GamesQueryCacheKey.BuildAnonymousCacheKey(plain);

        foreach (var property in properties)
        {
            // Several values rather than one, because some of these are normalised
            // before they reach the store: any sort order that is not "asc" means
            // descending, so a single arbitrary probe would read a correct
            // normalisation as a missing field. What has to hold is that the
            // property is capable of changing the key at all.
            var cacheable = QueryProbes.Candidates(property.PropertyType)
                .Select(value => Query(property, value))
                .Where(GamesQueryCacheKey.IsCacheableAnonymousQuery)
                .ToList();

            if (cacheable.Count == 0)
            {
                continue;
            }

            cacheable.Select(GamesQueryCacheKey.BuildAnonymousCacheKey).Should().Contain(
                key => key != plainKey,
                $"no value of {property.Name} that this cache agrees to serve changes the key " +
                "it is served under, so listings that differ in it share one entry and " +
                "whichever arrives second is handed the first one's page");
        }
    }

    private static GamesQuery Query(PropertyInfo property, object value)
    {
        var query = new GamesQuery();
        property.SetValue(query, value);

        return query;
    }

    /// <summary>
    /// The two directions of a sort are two listings.
    /// </summary>
    /// <remarks>
    /// The repository reads the order in every branch, the default one included.
    /// </remarks>
    [Fact]
    public void TellTheTwoDirectionsOfASortApart()
    {
        var ascending = GamesQueryCacheKey.BuildAnonymousCacheKey(new GamesQuery { SortOrder = "asc" });
        var descending = GamesQueryCacheKey.BuildAnonymousCacheKey(new GamesQuery { SortOrder = "desc" });

        ascending.Should().NotBe(descending,
            "these are opposite ends of the same list, and the field that reverses it is read " +
            "whether or not a sort field came with it");
    }

    /// <summary>
    /// Both spellings of the same listing are one entry.
    /// </summary>
    /// <remarks>
    /// The repository lowercases the sort field and reads the statuses as a set.
    /// </remarks>
    [Fact]
    public void HoldOneEntryForTwoSpellingsOfTheSameListing()
    {
        var lower = new GamesQuery
        {
            SortBy = "title",
            SortOrder = "asc",
            Statuses = [ModuleStatus.Active, ModuleStatus.Draft],
        };
        var upper = new GamesQuery
        {
            SortBy = "Title",
            SortOrder = "ASC",
            Statuses = [ModuleStatus.Draft, ModuleStatus.Active],
        };

        GamesQueryCacheKey.BuildAnonymousCacheKey(upper).Should().Be(GamesQueryCacheKey.BuildAnonymousCacheKey(lower),
            "the repository lowercases the sort field and matches the statuses as a set, so " +
            "these are one listing and two entries of it are one wasted");
    }
}
