using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Repositories.Personal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Personal;

/// <summary>
/// The counter join answers the number the correlated count answered, including
/// for the users it has no row for.
/// </summary>
/// <remarks>
/// A <c>Count()</c> correlated to the user row returns zero for a user who runs no
/// games; a join returns him only if it is a left one. The distinction is invisible
/// in the sorted-by-nothing case and total in the sorted case: an inner join silently
/// removes from <c>GET /v1/users</c> everybody the sort is about to put at the far
/// end, which is most of the registry.
/// </remarks>
public class UserCountJoinShould
{
    private static readonly Guid MasterId = Guid.Parse("5f3a2b40-0000-4000-8000-000000000001");
    private static readonly Guid AssistantId = Guid.Parse("5f3a2b40-0000-4000-8000-000000000002");
    private static readonly Guid IdlerId = Guid.Parse("5f3a2b40-0000-4000-8000-000000000003");

    private static readonly DateTimeOffset Created = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
        CreatedUtc = Created,
    };

    /// <summary>
    /// One user with two games of his own and an assistant seat in a third, one with
    /// an assistant seat only, and one with nothing at all.
    /// </summary>
    private static DmDbContext Seeded()
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        context.Users.AddRange(
            NewUser(MasterId, "master"),
            NewUser(AssistantId, "assistant"),
            NewUser(IdlerId, "idler"));

        var third = Guid.NewGuid();
        context.Games.AddRange(
            new DbGame { GameId = Guid.NewGuid(), PublicId = "aaaaa", MasterId = MasterId, Title = "Раз", CreatedUtc = Created },
            new DbGame { GameId = Guid.NewGuid(), PublicId = "bbbbb", MasterId = MasterId, Title = "Два", CreatedUtc = Created },
            new DbGame { GameId = third, PublicId = "ccccc", MasterId = IdlerId, Title = "Три", CreatedUtc = Created, IsRemoved = true });

        context.GameAssistants.AddRange(
            new DbGameAssistant { GameAssistantId = Guid.NewGuid(), GameId = third, UserId = MasterId, JoinedUtc = Created },
            new DbGameAssistant { GameAssistantId = Guid.NewGuid(), GameId = third, UserId = AssistantId, JoinedUtc = Created });

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CountZeroForAUserTheCounterHasNoRowFor()
    {
        using var context = Seeded();

        var counted = await UserCountQueries
            .WithCount(context.Users, UserCountQueries.GamesMastered(context))
            .ToDictionaryAsync(x => x.User.Username, x => x.Count);

        counted.Should().HaveCount(3, "the join is a left one and drops nobody");
        counted["master"].Should().Be(2);
        counted["assistant"].Should().Be(0, "he masters nothing, and nothing is zero rather than absent");
        counted["idler"].Should().Be(0, "his only game is deleted, so the counter has no row for him either");
    }

    [Fact]
    public async Task AddTheTwoHalvesOfHosting()
    {
        using var context = Seeded();

        var counted = await UserCountQueries
            .WithCountSum(context.Users,
                UserCountQueries.GamesMastered(context), UserCountQueries.GamesAssisted(context))
            .ToDictionaryAsync(x => x.User.Username, x => x.Count);

        counted.Should().HaveCount(3, "both joins are left ones, and one missing half is not a missing user");
        counted["master"].Should().Be(2,
            "two games of his own; the third is deleted, so his seat in it is not a game he runs");
        counted["assistant"].Should().Be(0, "his only seat is in the deleted game");
        counted["idler"].Should().Be(0);
    }

    [Fact]
    public async Task OrderByTheCounterAndBreakTiesByName()
    {
        using var context = Seeded();

        var descending = await UserCountQueries.Order(
                UserCountQueries.WithCountSum(context.Users,
                    UserCountQueries.GamesMastered(context), UserCountQueries.GamesAssisted(context)),
                false)
            .Select(x => x.User.Username)
            .ToArrayAsync();

        descending.Should().Equal(["master", "assistant", "idler"],
            "the busiest first, and the two who run nothing in alphabetical order after him");
    }

    [Fact]
    public async Task LeaveTheBoundsToOneJoin()
    {
        using var context = Seeded();

        var inRange = await UserCountQueries.InRange(
                UserCountQueries.WithCountSum(context.Users,
                    UserCountQueries.BlogsOwned(context), UserCountQueries.BlogsAssisted(context)),
                0, 0)
            .Select(u => u.Username)
            .ToArrayAsync();

        inRange.Should().BeEquivalentTo(["master", "assistant", "idler"],
            "nobody owns a blog, and a range that admits zero admits everybody");
    }

    [Fact]
    public async Task CountTheBlogsItsOwnerHas()
    {
        using var context = Seeded();
        context.Blogs.Add(new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = "blogx",
            AuthorId = AssistantId,
            Title = "Дневник",
            CreatedUtc = Created,
        });
        await context.SaveChangesAsync();

        var inRange = await UserCountQueries.InRange(
                UserCountQueries.WithCountSum(context.Users,
                    UserCountQueries.BlogsOwned(context), UserCountQueries.BlogsAssisted(context)),
                1, null)
            .Select(u => u.Username)
            .ToArrayAsync();

        inRange.Should().Equal(["assistant"], "he is the only one with a blog to his name");
    }
}
