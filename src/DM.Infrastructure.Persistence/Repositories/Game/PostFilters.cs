using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Repositories.Game;

internal class PostWithRating
{
    public required Entities.Game.Posts.Post Post { get; init; }
    public int Rating { get; init; }
    public int ReviewCount { get; init; }
    public DateTimeOffset? LastReviewUtc { get; init; }
}

internal static class PostFilters
{
    /// <summary>
    /// The rating of a post, the sum of the signs of its live reviews. Sum over no
    /// rows is NULL in SQL, hence the nullable cast and the coalesce.
    /// </summary>
    /// <remarks>Declared before <see cref="WithRating" />, which expands it at type load.</remarks>
    internal static readonly Expression<Func<DbPost, int>> RatingOf =
        p => p.Reviews.Where(r => !r.IsRemoved).Sum(r => (int?)r.SignValue) ?? 0;

    /// <summary>
    /// The review figures a list filters and sorts by, beside the post. EF Core
    /// translates each aggregate into a correlated subquery, but the subqueries share
    /// a single filter (!IsRemoved) so the query planner can fuse them — net cost on
    /// a 20-row page is three LEFT JOIN LATERALs, not 60 independent round-trips.
    /// </summary>
    /// <remarks>
    /// Navigation aggregates, not a GROUP BY subquery with a null ternary: EF cannot
    /// translate that shape in the CountAsync path.
    /// </remarks>
    internal static readonly Expression<Func<DbPost, PostWithRating>> WithRating =
        ExpressionSplicer.Expand<Func<DbPost, PostWithRating>>(p => new PostWithRating
        {
            Post = p,
            Rating = RatingOf.Splice(p),
            ReviewCount = p.Reviews.Count(r => !r.IsRemoved),
            LastReviewUtc = p.Reviews.Where(r => !r.IsRemoved).Max(r => (DateTimeOffset?)r.CreatedUtc)
        });

    /// <summary>
    /// The filters on what a post says, who wrote it and when: the same on a room's
    /// list and on the rated feed, so one name means one thing on both.
    /// </summary>
    /// <remarks>
    /// <paramref name="authorIds" /> is what the names mean when "Аккаунт удален" is
    /// among them - the removed accounts and the live ones named alongside
    /// (<see cref="RemovedAccountScope" />) - and null otherwise.
    /// </remarks>
    internal static IQueryable<DbPost> WhereWrittenBy(
        IQueryable<DbPost> posts,
        string? search,
        IReadOnlyCollection<string>? authorUsernames,
        IReadOnlyCollection<Guid>? authorIds,
        DateTimeOffset? createdFromUtc,
        DateTimeOffset? createdToUtc)
    {
        // Matches the visible text of the post, not the body: cutting a hidden block
        // out of the body leaves whitespace a phrase search can detect, and the
        // projection renders a post with and without the block to the same text.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePatterns.Contains(search);
            posts = posts.Where(p => EF.Functions.ILike(
                EF.Property<string>(p, "SearchText"), pattern));
        }

        // Author filter (repeated parameter, materialised before LINQ). The profile of
        // removed accounts asks by the author column, joining no account.
        if (authorIds is not null)
        {
            posts = posts.Where(p => authorIds.Contains(p.AuthorId));
        }
        else if (authorUsernames is { Count: > 0 })
        {
            var usernames = authorUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .ToArray();
            if (usernames.Length > 0)
            {
                // Both reads lift the account filter to keep the posts of removed
                // accounts (RemovedAuthors); a removed login matches nothing, or
                // the old name would find what the removal took off it.
                posts = posts.Where(p => !p.Author.IsRemoved && usernames.Contains(p.Author.Username));
            }
        }

        // Post creation date filters
        if (createdFromUtc.HasValue)
        {
            var from = createdFromUtc.Value;
            posts = posts.Where(p => p.CreatedUtc >= from);
        }
        if (createdToUtc.HasValue)
        {
            posts = posts.WhereAtOrBefore(p => p.CreatedUtc, createdToUtc.Value);
        }

        return posts;
    }

    /// <summary>
    /// The filters on how a post was received. Rating can be negative.
    /// </summary>
    internal static IQueryable<PostWithRating> WhereRated(
        IQueryable<PostWithRating> posts, bool? hasReviews, int? minRating, int? maxRating)
    {
        posts = hasReviews switch
        {
            true => posts.Where(x => x.ReviewCount > 0),
            false => posts.Where(x => x.ReviewCount == 0),
            null => posts
        };

        if (minRating.HasValue)
        {
            var min = minRating.Value;
            posts = posts.Where(x => x.Rating >= min);
        }

        if (maxRating.HasValue)
        {
            var max = maxRating.Value;
            posts = posts.Where(x => x.Rating <= max);
        }

        return posts;
    }
}
