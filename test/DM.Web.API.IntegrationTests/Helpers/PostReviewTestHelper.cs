using Microsoft.EntityFrameworkCore;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Web.API.IntegrationTests.Helpers;

public static class PostReviewTestHelper
{
    // IgnoreQueryFilters: a removed row is hidden by the global soft-delete filter.
    public static async Task RemoveReview(DatabaseFixture dbFixture, Guid reviewId)
    {
        await using var db = dbFixture.CreateDbContext();
        await db.PostReviews.IgnoreQueryFilters()
            .Where(r => r.PostReviewId == reviewId).ExecuteDeleteAsync();
    }

    public static async Task<DbPostReview?> StoredReview(DatabaseFixture dbFixture, Guid reviewId)
    {
        await using var db = dbFixture.CreateDbContext();
        return await db.PostReviews.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.PostReviewId == reviewId);
    }

    public static async Task<int> QualityRatingOf(DatabaseFixture dbFixture, Guid userId)
    {
        await using var db = dbFixture.CreateDbContext();
        return await db.Users.Where(u => u.UserId == userId).Select(u => u.QualityRating).FirstAsync();
    }
}
