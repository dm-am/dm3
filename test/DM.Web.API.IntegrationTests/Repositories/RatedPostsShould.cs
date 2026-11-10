using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

// The ordering comes from a GroupBy only Postgres translates, so the production query runs here,
// joins included.
public class RatedPostsShould : PostRepositoryTestBase
{
    public RatedPostsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task DefaultSortRatedPostsByRatingDescending()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var lowRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 1);
        var highRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 3);

        // No SortBy: the repository default is rating desc.
        // Scoped to this test's own game — the fixture database is seeded and
        // shared, so an unscoped query would rank against everyone else's posts.
        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        total.Should().Be(2);
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(highRated);
        ordered[0].Rating.Should().Be(3);
        ordered[1].Id.Should().Be(lowRated);
        ordered[1].Rating.Should().Be(1);
    }

    /// <summary>
    /// hasReviews filters both ways, as on a room's list: false asks for the posts nobody reviewed.
    /// </summary>
    [Fact]
    public async Task KeepOnlyThePostsNobodyReviewedWhenAskedForNoReviews()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddRatedPostAsync(dbContext, context, positiveReviews: 2);
        // A removed review is no review: the post counts as not reviewed.
        var unreviewed = await AddRatedPostAsync(dbContext, context, positiveReviews: 0, removedReviews: 1);

        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            HasReviews = false,
        }, Guid.Empty);

        total.Should().Be(1);
        posts.Select(p => p.Id).Should().Equal(unreviewed);
    }

    [Fact]
    public async Task SortByLastReviewOrdersByMostRecentReview()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        // The higher-rated post was reviewed earlier; the lower-rated one got a
        // more recent review. lastreview must invert the rating-desc order.
        var highRatedOlderReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 3, reviewTime: DateTimeOffset.UtcNow.AddHours(-1));
        var lowRatedNewerReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 1, reviewTime: DateTimeOffset.UtcNow);

        var (posts, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            SortBy = "lastreview",
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(lowRatedNewerReview);
        ordered[1].Id.Should().Be(highRatedOlderReview);
    }

    // The homepage block sends sortBy=rating with createdFromUtc=Monday, cutting on the post's
    // creation date.
    [Fact]
    public async Task KeepPostsCreatedBeforeTheWeekOutOfTheWeeklyRating()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // The same boundary the client (getWeekStartUtc) and the seeder
        // (DataSeeder.WeekStartUtc) compute: Monday 00:00 UTC.
        var now = DateTimeOffset.UtcNow;
        var weekStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero)
            .AddDays(-(((int)now.DayOfWeek + 6) % 7));

        var context = await AddGameWithRoomAsync(dbContext);
        // Higher rated, but written before the week started.
        var lastWeek = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 12,
            postCreatedUtc: weekStart.AddSeconds(-1));
        // The showcase post: fewer reviews, written inside the window.
        var thisWeek = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 7,
            postCreatedUtc: weekStart);

        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            SortBy = "rating",
            HasReviews = true,
            CreatedFromUtc = weekStart,
            GameId = context.GameId,
        }, Guid.Empty);

        var ordered = posts.ToList();
        total.Should().Be(1);
        ordered.Should().ContainSingle()
            .Which.Id.Should().Be(thisWeek, "the week's best is the best OF THE WEEK");
        ordered.Should().NotContain(p => p.Id == lastWeek);
    }

    [Fact]
    public async Task ReportTheViewerAsAReaderOfTheGameTheySubscribeTo()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddRatedPostAsync(dbContext, context, positiveReviews: 1);
        var subscriberId = await AddSubscriberAsync(dbContext, context.GameId);

        var (forSubscriber, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, subscriberId);

        // The attached game is what GameLink builds its tooltip from, and its
        // participation list is derived from this flag. The feed itself is public,
        // so the game is loaded anonymously — filling the flag is the only thing
        // the viewer's identity is used for here.
        forSubscriber.Single().Room!.Game!.IsViewerSubscriber.Should().BeTrue();

        var (forStranger, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.NewGuid());

        forStranger.Single().Room!.Game!.IsViewerSubscriber.Should().BeFalse();

        var (forGuest, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        }, Guid.Empty);

        // A guest subscribes to nothing, and no lookup is made for them.
        forGuest.Single().Room!.Game!.IsViewerSubscriber.Should().BeFalse();
    }

    private static async Task<Guid> AddSubscriberAsync(DmDbContext dbContext, Guid gameId)
    {
        var subscriberId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = subscriberId,
            Username = $"sub{subscriberId:N}"[..20],
            Email = $"{subscriberId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Subscriptions.Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = subscriberId,
            TargetType = SubscriptionTargetType.Game,
            TargetId = gameId,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return subscriberId;
    }
}
