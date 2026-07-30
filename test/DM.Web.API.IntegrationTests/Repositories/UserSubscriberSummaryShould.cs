using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The per-category subscriber counts on a user read.
/// </summary>
/// <remarks>
/// Runs against the container Postgres: the counts are
/// count(*) FILTER (WHERE "Settings" &amp; bit &lt;&gt; 0) aggregates folded into the
/// same GROUP BY as the preview, and neither the FILTER form nor the bitwise
/// predicate inside an aggregate is exercised by an in-memory provider.
///
/// The case worth pinning is the one the profile got wrong: the preview is capped
/// at <see cref="SubscriptionPolicy.PreviewCap" /> and ranked by activity before
/// anything knows about categories, so a category whose subscribers all sort
/// below the cap contributes no name at all. Its count still has to be right —
/// that number is the only thing standing between the reader and a line that
/// silently claims nobody is subscribed.
/// </remarks>
public class UserSubscriberSummaryShould : IntegrationTestBase
{
    public UserSubscriberSummaryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CountEachCategoryEvenWhenNoneOfItsSubscribersReachThePreview()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var target = await AddUserAsync(dbContext, "target", lastActivityUtc: DateTimeOffset.UtcNow);

        // Enough game subscribers with recorded activity to fill the preview on
        // their own, so the blog subscriber below cannot reach it.
        var recentlyActive = DateTimeOffset.UtcNow;
        for (var i = 0; i < SubscriptionPolicy.PreviewCap; i++)
        {
            var subscriber = await AddUserAsync(
                dbContext, $"games{i}", recentlyActive.AddMinutes(-i));
            AddSubscription(dbContext, subscriber, target, SubscriptionSettings.AuthorGameEvents);
        }

        // One blog subscriber who has never been active. The preview orders
        // never-active last, so this row is below the cap by construction.
        var blogSubscriber = await AddUserAsync(dbContext, "blogs0", lastActivityUtc: null);
        AddSubscription(dbContext, blogSubscriber, target, SubscriptionSettings.AuthorBlogEvents);

        await dbContext.SaveChangesAsync();

        var user = (await repository.GetUsersAsync(new[] { target })).Single();

        user.Subscribers.Should().HaveCount(SubscriptionPolicy.PreviewCap,
            "the preview is capped");
        user.Subscribers.Should().NotContain(s => s.Username == "blogs0",
            "a never-active subscriber sorts below the cap");

        // The point: the blog line has a subscriber and the preview cannot show
        // it, so the count is what tells the reader it exists.
        user.SubscribersByCategory.Games.Should().Be(SubscriptionPolicy.PreviewCap);
        user.SubscribersByCategory.Blogs.Should().Be(1);
        user.SubscribersByCategory.Topics.Should().Be(0);
    }

    [Fact]
    public async Task CountOneSubscriberOncePerCategoryTheyAskedFor()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var target = await AddUserAsync(dbContext, "multi", lastActivityUtc: DateTimeOffset.UtcNow);
        var subscriber = await AddUserAsync(dbContext, "everything", DateTimeOffset.UtcNow);

        // One subscription carrying all three category bits — the popover's
        // default. The three counts are independent conditions over the same
        // row, so each must see it.
        AddSubscription(dbContext, subscriber, target,
            SubscriptionSettings.AuthorGameEvents |
            SubscriptionSettings.AuthorBlogEvents |
            SubscriptionSettings.AuthorTopicEvents);
        await dbContext.SaveChangesAsync();

        var user = (await repository.GetUsersAsync(new[] { target })).Single();

        user.SubscribersByCategory.Games.Should().Be(1);
        user.SubscribersByCategory.Blogs.Should().Be(1);
        user.SubscribersByCategory.Topics.Should().Be(1);
    }

    [Fact]
    public async Task LeaveEveryCategoryAtZeroForAUserNobodySubscribesTo()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var target = await AddUserAsync(dbContext, "lonely", DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        var user = (await repository.GetUsersAsync(new[] { target })).Single();

        // No summary row at all for this user, which is a different path through
        // the assignment than a row of zeroes.
        user.SubscribersByCategory.Games.Should().Be(0);
        user.SubscribersByCategory.Blogs.Should().Be(0);
        user.SubscribersByCategory.Topics.Should().Be(0);
        user.Subscribers.Should().BeEmpty();
    }

    /// <summary>
    /// A fresh user. The fixture database is shared and seeded, so identifiers
    /// and usernames have to be unique per test run.
    /// </summary>
    private static async Task<Guid> AddUserAsync(
        DmDbContext dbContext, string prefix, DateTimeOffset? lastActivityUtc)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = lastActivityUtc,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static void AddSubscription(
        DmDbContext dbContext, Guid subscriberId, Guid targetId, SubscriptionSettings settings)
    {
        dbContext.Subscriptions.Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = subscriberId,
            TargetType = SubscriptionTargetType.User,
            TargetId = targetId,
            Settings = settings,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
    }
}
