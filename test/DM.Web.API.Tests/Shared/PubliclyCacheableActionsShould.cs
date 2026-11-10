using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Only a response that is the same for every caller may be stored by a shared
/// cache.
/// </summary>
/// <remarks>
/// Caches key on method and path, and nothing declares a Vary. Location defaults to Any, so a bare
/// Duration is public. The list is lookups never handed an identity, asserted both ways.
/// </remarks>
public class PubliclyCacheableActionsShould
{
    private static readonly string[] Catalogues =
    [
        "AchievementController.GetAchievementCategories",
        "AchievementController.GetAchievementTypes",
        "AwardController.GetAwardTypes",
        "AwardController.GetContestSeries",
        "GameController.GetTags",
    ];

    private static (string Name, ResponseCacheAttribute? Policy)[] Actions() => ApiSurface.RoutedActions()
        .Select(m => (
            Name: ApiSurface.Named(m),
            Policy: m.GetCustomAttribute<ResponseCacheAttribute>()
                    ?? m.DeclaringType!.GetCustomAttribute<ResponseCacheAttribute>()))
        .ToArray();

    // Location Any is the one value that puts "public" on the wire.
    private static bool StoredByASharedCache(ResponseCacheAttribute? policy) =>
        policy is { NoStore: false, Location: ResponseCacheLocation.Any };

    [Fact]
    public void BeNothingButTheListedCatalogues()
    {
        var actions = Actions();
        actions.Should().NotBeEmpty("the scan has to find the actions of the host");

        var offenders = actions
            .Where(action => StoredByASharedCache(action.Policy) && !Catalogues.Contains(action.Name))
            .Select(action => action.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a shared cache keys on the method and the path alone, so one personal " +
            "field makes the first caller's answer everyone's, see the class remarks");
    }

    // Location.Client keeps a badge in the browser past the request that clears it; an impersonal
    // response belongs above.
    [Fact]
    public void NotCacheInTheBrowserEither()
    {
        var offenders = Actions()
            .Where(action => action.Policy is { NoStore: false, Location: ResponseCacheLocation.Client })
            .Select(action => action.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a personal answer held in the browser is stale for its own reader, " +
            "see the class remarks");
    }

    [Fact]
    public void LeaveNoStaleNameOnTheList()
    {
        var publiclyCached = Actions()
            .Where(action => StoredByASharedCache(action.Policy))
            .Select(action => action.Name)
            .ToArray();

        Catalogues.Should().BeSubsetOf(publiclyCached,
            "an entry that outlives the action it exempts is an exemption nobody " +
            "reviewed, waiting for a new action to be given the same name");
    }
}
