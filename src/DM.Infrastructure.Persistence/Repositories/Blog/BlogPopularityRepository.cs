using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc />
internal class BlogPopularityRepository : IBlogPopularityRepository
{
    /// <summary>Blogs per write batch, so one pass cannot hold the store for minutes.</summary>
    private const int BatchSize = 100;

    private readonly DmDbContext _dbContext;

    /// <inheritdoc cref="BlogPopularityRepository" />
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
        await _dbContext.Subscriptions
            .TagWith("DM.Blog.ActiveReaderCounts")
            .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                        blogIds.Contains(s.TargetId) &&
                        s.Subscriber.LastActivityUtc.HasValue &&
                        s.Subscriber.LastActivityUtc.Value > activeSince)
            .GroupBy(s => s.TargetId)
            .Select(g => new { BlogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BlogId, x => x.Count, cancellationToken);

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
                blog.PopularityScoreUpdatedUtc = calculatedUtc;
                updated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}
