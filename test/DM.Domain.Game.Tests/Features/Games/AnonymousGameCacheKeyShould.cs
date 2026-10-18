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
/// The entry is shared by every anonymous reader of the site, so a field that
/// changes what the repository returns and does not change the key hands one
/// reader another reader's page for the whole life of the entry. That is what
/// happened to the sort order: it was written into the key only when a sort field
/// came with it, while the repository reads the order in every branch including
/// the default one, so ?sortOrder=asc and ?sortOrder=desc were one entry.
///
/// Walked by reflection rather than listed, because a list is exactly what failed
/// here. Twenty-three properties, two hand-kept mirrors of them, and nothing that
/// notices a twenty-fourth: the field is added, both mirrors are forgotten, and
/// the site keeps answering — with the wrong page, to somebody who has no way of
/// knowing. Every property has to end up in one of two places, and this says
/// which without needing anyone to remember either.
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
        GameService.IsCacheableAnonymousQuery(plain).Should().BeTrue(
            "the plainest listing there is - the front page of an anonymous reader - is the " +
            "one this cache exists for");
        var plainKey = GameService.BuildAnonymousCacheKey(plain);

        foreach (var property in properties)
        {
            // Several values rather than one, because some of these are normalised
            // before they reach the store: any sort order that is not "asc" means
            // descending, so a single arbitrary probe would read a correct
            // normalisation as a missing field. What has to hold is that the
            // property is capable of changing the key at all.
            var cacheable = QueryProbes.Candidates(property.PropertyType)
                .Select(value => Query(property, value))
                .Where(GameService.IsCacheableAnonymousQuery)
                .ToList();

            if (cacheable.Count == 0)
            {
                continue;
            }

            cacheable.Select(GameService.BuildAnonymousCacheKey).Should().Contain(
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
    /// The defect itself, written out. The order was appended to the key only
    /// beside a sort field, and the repository reads it in every branch - so the
    /// oldest games and the newest games were one entry, and for a minute at a
    /// time the front page showed whichever of them was asked for first.
    /// </remarks>
    [Fact]
    public void TellTheTwoDirectionsOfASortApart()
    {
        var ascending = GameService.BuildAnonymousCacheKey(new GamesQuery { SortOrder = "asc" });
        var descending = GameService.BuildAnonymousCacheKey(new GamesQuery { SortOrder = "desc" });

        ascending.Should().NotBe(descending,
            "these are opposite ends of the same list, and the field that reverses it is read " +
            "whether or not a sort field came with it");
    }

    /// <summary>
    /// Both spellings of the same listing are one entry.
    /// </summary>
    /// <remarks>
    /// The other direction, and the cheaper half: the repository lowercases the
    /// sort field and reads the statuses as a set, so keys differing only in the
    /// case of one and the order of the other held byte-identical pages under two
    /// names — twice the memory and half the hit rate for nothing.
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

        GameService.BuildAnonymousCacheKey(upper).Should().Be(GameService.BuildAnonymousCacheKey(lower),
            "the repository lowercases the sort field and matches the statuses as a set, so " +
            "these are one listing and two entries of it are one wasted");
    }
}
