using System;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Users;
using Xunit;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>
/// Only the reads that make up the profile of removed accounts know its name, and only
/// they read the removed rows.
/// </summary>
/// <remarks>
/// The name opens one profile and narrows its tabs; elsewhere it is unknown. The callers are named
/// here, so a new reader of the name or of the scope past the filter is a decision.
/// </remarks>
public class RemovedAccountReachShould
{
    private static readonly ArchitectureModel Solution = ArchitectureSolution.Model;

    /// <summary>
    /// The profile, the host, author and player filters of its tabs, the reservation of
    /// the name, and the refusal to restore a row that carries it.
    /// </summary>
    private static readonly string[] MayKnowTheName =
    [
        "DM.Web.API.Features.Community.Users.CommunityUserApiService",
        "DM.Domain.Community.Features.Profiles.CommunityProfileService",
        "DM.Infrastructure.Persistence.Repositories.Personal.UserRepository",
        "DM.Infrastructure.Persistence.Repositories.Game.GameRepository",
        "DM.Infrastructure.Persistence.Repositories.Forum.TopicRepository",
        "DM.Infrastructure.Persistence.Repositories.Game.PostRepository",
        "DM.Domain.Blog.Features.Blogs.BlogService",
        "DM.Domain.Blog.Features.Publications.PublicationService",
        "DM.Infrastructure.Persistence.Shared.Users.AccountReservation",
        "DM.Infrastructure.Persistence.Shared.Users.RemovedAccountScope",
        "DM.Domain.Account.Features.Deactivation.DeactivationService",
    ];

    private const string Scope = "DM.Infrastructure.Persistence.Shared.Users.RemovedAccountScope";

    /// <summary>The reads that may go past the account filter for the removed rows.</summary>
    private static readonly string[] MayReadTheRemovedRows =
    [
        "DM.Infrastructure.Persistence.Repositories.Personal.UserRepository",
        "DM.Infrastructure.Persistence.Repositories.Game.GameRepository",
        "DM.Infrastructure.Persistence.Repositories.Forum.TopicRepository",
        "DM.Infrastructure.Persistence.Repositories.Game.PostRepository",
    ];

    /// <summary>
    /// The production types that use <paramref name="fullName" />, each by its declaring type: async
    /// methods and lambdas compile into nested classes.
    /// </summary>
    private static string[] DependentsOf(string fullName) => Solution.Classes
        .Where(c => c.Assembly.Name.StartsWith("DM.", StringComparison.Ordinal) &&
                    !c.Assembly.Name.EndsWith(".Tests", StringComparison.Ordinal))
        .Where(c => c.Dependencies.Any(d => d.Target.FullName == fullName))
        .Select(c => c.FullName.Split('+', '/')[0])
        .Where(name => name != fullName)
        .Distinct()
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void BeKnownByTheProfileAndItsListsAlone()
    {
        var knowing = DependentsOf(typeof(RemovedAccount).FullName!);

        knowing.Should().NotBeEmpty("the profile itself asks for the name, and a walk that finds nothing proves nothing");
        knowing.Except(MayKnowTheName).Should().BeEmpty(
            "every other endpoint answers \"Аккаунт удален\" as a name nobody holds; teaching " +
            "one more to recognise it is a decision about what the profile of removed accounts shows");
    }

    [Fact]
    public void BeReadPastTheFilterByTheProfileAndItsListsAlone()
    {
        var reading = DependentsOf(Scope);

        reading.Should().NotBeEmpty("the profile sums the removed rows through the scope");
        reading.Except(MayReadTheRemovedRows).Should().BeEmpty(
            "each caller of the scope is one more read past the account filter, and its " +
            "identifiers must narrow a list on the server and never reach an answer");
    }
}
