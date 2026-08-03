using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Only a response that is the same for every caller may be stored by a shared
/// cache.
/// </summary>
/// <remarks>
/// The server cache and every proxy on the way key on the method and the path,
/// and on nothing else, because nothing declares a Vary. One personal field in
/// the body, an unread counter, a "mine" flag, a list that depends on the
/// audience, is therefore enough to make the answer given to the first caller the
/// answer given to all of them. Not a scenario: the forum handed one user's
/// unread counters to everyone, and the repair was one word inside an attribute.
///
/// API_DESIGN states the rule, this keeps it true. The attribute is written by
/// copying the neighbouring action, and a catalogue endpoint is a convenient
/// neighbour. Note also that Location defaults to Any, so an attribute that names
/// only a Duration is public without saying so, and is matched here as such.
///
/// The list below is the whole exception: lookups whose service is never handed
/// an identity. It is asserted in both directions. An action that starts caching
/// publicly without being added turns the first test red, and a name that stops
/// caching publicly, or stops existing, turns the second red, because an
/// exemption nobody has to maintain outlives the reason it was granted.
/// </remarks>
public class PubliclyCacheableActionsShould
{
    /// <summary>The same rows for an anonymous caller and for a moderator.</summary>
    private static readonly string[] Catalogues =
    [
        "AchievementController.GetAchievementCategories",
        "AchievementController.GetAchievementTypes",
        "AwardController.GetAwardTypes",
        "AwardController.GetContestSeries",
        "GameController.GetTags",
    ];

    /// <summary>
    /// Every controller action in the host with the cache policy in force for it.
    /// The action's own attribute wins over the controller's, the way MVC resolves
    /// it.
    /// </summary>
    private static (string Name, ResponseCacheAttribute? Policy)[] Actions() => typeof(Startup).Assembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
        .Select(m => (
            Name: $"{m.DeclaringType!.Name}.{m.Name}",
            Policy: m.GetCustomAttribute<ResponseCacheAttribute>()
                    ?? m.DeclaringType!.GetCustomAttribute<ResponseCacheAttribute>()))
        .ToArray();

    /// <summary>Location Any is the one value that puts "public" on the wire.</summary>
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
