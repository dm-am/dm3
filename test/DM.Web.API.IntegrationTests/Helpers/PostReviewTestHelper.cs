using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// What the review suites read back out of the database, and the cleanup that
/// takes their rows out again.
/// </summary>
public static class PostReviewTestHelper
{
    /// <summary>Takes one review out of the shared database.</summary>
    /// <remarks>
    /// IgnoreQueryFilters: a row a test has had removed is hidden by the global
    /// soft-delete filter, and the cleanup would leave it behind.
    /// </remarks>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="reviewId">Review to remove</param>
    public static async Task RemoveReview(DatabaseFixture dbFixture, Guid reviewId)
    {
        await using var db = dbFixture.CreateDbContext();
        await db.PostReviews.IgnoreQueryFilters()
            .Where(r => r.PostReviewId == reviewId).ExecuteDeleteAsync();
    }

    /// <summary>The stored review, soft-deleted ones included.</summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="reviewId">Review to read</param>
    public static async Task<DbPostReview?> StoredReview(DatabaseFixture dbFixture, Guid reviewId)
    {
        await using var db = dbFixture.CreateDbContext();
        return await db.PostReviews.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.PostReviewId == reviewId);
    }

    /// <summary>The counter a received review moves.</summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="userId">User whose rating is read</param>
    public static async Task<int> QualityRatingOf(DatabaseFixture dbFixture, Guid userId)
    {
        await using var db = dbFixture.CreateDbContext();
        return await db.Users.Where(u => u.UserId == userId).Select(u => u.QualityRating).FirstAsync();
    }
}
