using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc />
internal class BlogPopularityRepository : IBlogPopularityRepository
{
    // Blogs per write batch, so one pass cannot hold the store for minutes.
    private const int BatchSize = 100;

    private readonly DmDbContext _dbContext;

    public BlogPopularityRepository(DmDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Guid>> GetScorableBlogIds(CancellationToken cancellationToken = default) =>
        await _dbContext.Blogs
            .TagWith("DM.Blog.ScorableBlogs")
            .Where(b => !b.IsRemoved)
            .Select(b => b.BlogId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountActiveReaders(
        IReadOnlyCollection<Guid> blogIds,
        DateTimeOffset activeSince,
        CancellationToken cancellationToken = default) =>
        await SubscriberSummaries.ActiveByTarget(
            _dbContext.Subscriptions.TagWith("DM.Blog.ActiveReaderCounts"),
            SubscriptionTargetType.Blog, blogIds, activeSince, cancellationToken);

    /// <inheritdoc />
    public async Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken cancellationToken = default)
    {
        var updated = 0;

        foreach (var batch in scores.Keys.Chunk(BatchSize))
        {
            var blogs = await _dbContext.Blogs
                .TagWith("DM.Blog.ApplyPopularityScores")
                .Where(b => batch.Contains(b.BlogId))
                .ToListAsync(cancellationToken);

            foreach (var blog in blogs)
            {
                var score = scores[blog.BlogId];
                if (blog.PopularityScore == score)
                {
                    continue;
                }

                blog.PopularityScore = score;
                updated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}
