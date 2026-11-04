using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Features.Games;

/// <summary>
/// One signed-in viewer's listings, one cache entry each.
/// </summary>
/// <remarks>
/// Asked through the listing, so what is held is the key the cache is actually handed.
/// </remarks>
public class UserGameCacheKeyShould : GameServiceTestBase
{
    private readonly List<string> askedKeys = [];

    public UserGameCacheKeyShould()
    {
        blacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(currentUserId, UserBlacklistSettings.HideGames, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        // The page itself is empty: what is asked is the key it is filed under.
        cache
            .GetOrCreateAsync(Arg.Any<object>(), Arg.Any<Func<Task<(GameDto[], PagingData)>>>(), Arg.Any<TimeSpan>())
            .Returns(call =>
            {
                askedKeys.Add((string)call.ArgAt<object>(0));
                return (Array.Empty<GameDto>(), new PagingData(new PagingQuery(), 10, 0));
            });
    }

    /// <summary>
    /// Every field the viewer can set moves the key.
    /// </summary>
    /// <remarks>
    /// On any other listing the service fills <see cref="GamesQuery.ExcludedMasterIds"/> itself.
    /// </remarks>
    [Fact]
    public async Task TellApartEveryQueryTheViewerCanAsk()
    {
        var properties = QueryProbes.WritableProperties(typeof(GamesQuery));
        properties.Should().NotBeEmpty(
            "the query declares the fields this is about, and finding none passes everything");

        var plainKey = await KeyOf(Own());

        foreach (var property in properties)
        {
            var keys = new List<string>();
            foreach (var value in QueryProbes.Candidates(property.PropertyType))
            {
                keys.Add(await KeyOf(Own(property, value)));
            }

            keys.Should().NotBeEmpty($"{property.Name} has to be probed with some value to be checked at all");
            keys.Should().Contain(key => key != plainKey,
                $"no value of {property.Name} changes the key the viewer's page is cached under, so " +
                "two listings that differ in it share one entry and the second is handed the first one's page");
        }
    }

    /// <summary>
    /// The masters the viewer hides are part of the key on the listings they are
    /// hidden from.
    /// </summary>
    /// <remarks>
    /// Switching "Скрывать игры" on, or adding a master to the list, has to take
    /// effect on the next page and not after the entry runs out.
    /// </remarks>
    [Fact]
    public async Task TellApartTheListingsOfAViewerWhoHidesAMaster()
    {
        var hidesNobody = await KeyOf(new GamesQuery());

        blacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(currentUserId, UserBlacklistSettings.HideGames, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { Guid.Parse("5a1e0000-0000-4000-8000-0000000000b2") });
        var hidesOne = await KeyOf(new GamesQuery());

        hidesOne.Should().NotBe(hidesNobody,
            "the master is taken off this listing, so the page without him and the page with " +
            "him are two pages");
    }

    /// <summary>A listing of the games the viewer takes part in, with one field set.</summary>
    private static GamesQuery Own(PropertyInfo? property = null, object? value = null)
    {
        var query = new GamesQuery { Participating = true };
        property?.SetValue(query, value);

        return query;
    }

    private async Task<string> KeyOf(GamesQuery query)
    {
        askedKeys.Clear();
        await service.GetGamesAsync(query);

        askedKeys.Should().ContainSingle("a signed-in viewer's listing is read through the cache once");
        return askedKeys.Single();
    }
}
