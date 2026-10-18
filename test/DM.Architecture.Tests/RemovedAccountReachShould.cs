using System;
using System.IO;
using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
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
/// "Аккаунт удален" is no account. The name opens one profile - the sum of every removed
/// row - and narrows the lists its tabs are made of; everywhere else it is a name nobody
/// holds, answered with the 404 or the empty list any unknown name gets. The removed rows
/// behind it are read past the account filter by one scope, and the identifiers that
/// read returns narrow a list on the server and go nowhere else.
///
/// Both halves are easy to widen by accident and neither shows on a page: a new endpoint
/// that learns the name serves something about removed accounts nobody decided to serve,
/// and a new caller of the scope is a new read past the filter. So the callers are named
/// here, and a new one is a decision somebody takes on purpose.
/// </remarks>
public class RemovedAccountReachShould
{
    private static readonly ArchitectureModel Solution = new ArchLoader()
        .LoadFilteredDirectory(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Build();

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
    /// The production types that use <paramref name="fullName" />, each named by the type
    /// it is declared in: an async method or a lambda compiles into a nested class of its
    /// own, and it is the declaring type that made the call.
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
