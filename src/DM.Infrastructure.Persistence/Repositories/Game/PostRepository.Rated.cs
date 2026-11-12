using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// One page row of the rated-posts read: the post exactly as every other read
/// projects it, plus the two room fields that read alone shows.
/// </summary>
/// <remarks>
/// The post itself is not built here. It used to be - a second projection
/// written out by hand, which filled the text and the author and left
/// AuthorUserId, GameId, the master, the assistants and both private-text
/// overrides at their defaults, because nothing obliges an object initializer
/// to mention a field. Every one of those defaults is an empty id or a false,
/// and the renderer read them as "the game has a lead whose id is empty" -
/// which is the id an anonymous reader carries. The feed served [private]
/// blocks to guests.
///
/// So the formula is spliced in from <see cref="PostMappers.PostProjection"/>
/// instead of restated: whatever the post's rendering needs to know, this read
/// knows too, and a field added there cannot go missing here.
/// </remarks>
internal class RatedPostRow
{
    public required Post Post { get; init; }
    public int RoomNumber { get; init; }
    public required string RoomTitle { get; init; }
}

internal partial class PostRepository
{
    /// <summary>
    /// Page row of <see cref="GetRated"/>. Expanded once, into a static, because
    /// the splice marker is a rewrite instruction and not a call: the expression
    /// has to be rewritten before EF ever sees it.
    /// </summary>
    private static readonly Expression<Func<PostWithRating, RatedPostRow>> RatedPostRowProjection =
        ExpressionSplicer.Expand<Func<PostWithRating, RatedPostRow>>(
            x => new RatedPostRow
            {
                Post = PostMappers.PostProjection.Splice(x.Post),
                RoomNumber = x.Post.Room.RoomNumber,
                RoomTitle = x.Post.Room.Title
            });

    public async Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query, Guid viewerId)
    {
        var sortedQuery = await RatedPosts(query);

        var totalCount = await sortedQuery.CountAsync();

        var rawData = await sortedQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(RatedPostRowProjection)
            .ToArrayAsync();

        var posts = rawData.Select(ToRatedPost).ToArray();

        // One batched query (IN(characterIds)) for avatar URLs, instead
        // of a correlated Uploads subquery per row in the main projection.
        await EnrichWithCharacterPictures(posts);
        await EnrichWithAttachments(posts);

        await AttachGames(posts, viewerId);

        return (posts, totalCount);
    }

    /// <summary>
    /// The posts of the feed the query admits, each with its rating, in the order
    /// asked. Shared by the count and the page.
    /// </summary>
    internal async Task<IOrderedQueryable<PostWithRating>> RatedPosts(PostsQuery query)
    {
        // Read-only path feeding the home-page widgets (best of week, latest
        // featured, Pulse). PostWithRating holds an entity reference, but no
        // entity is ever materialised: the terminals are CountAsync and a
        // projection into RatedPostRow, so nothing here reaches the change tracker
        // either way. AsNoTracking stays as the default of a read path.
        // See PERFORMANCE.md → "AsNoTracking".
        // The account filter is lifted to keep the posts of removed accounts,
        // signed by nobody (RemovedAuthors); the page projects through the post
        // formula itself, not through ProjectToPost.
        var baseQuery = _dbContext.Posts
            .KeepingRemovedAuthors()
            .AsNoTracking()
            .TagWith("DM.Game.PostsRated")
            .Where(p => !p.IsRemoved)
            .Where(p => p.Room.AccessType == RoomAccessType.Open)
            .Where(p => !p.Room.IsRemoved)
            .Where(p => !p.Room.Game.IsRemoved)
            .Where(p => p.Room.Game.Status != ModuleStatus.Draft)
            .Where(p => p.Room.Game.PremoderationStatus == PremoderationStatus.Approved);

        // Game filter
        if (query.GameId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.Room.GameId == query.GameId.Value);
        }

        baseQuery = PostFilters.WhereWrittenBy(baseQuery, query.Search, query.AuthorUsernames,
            await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames),
            query.CreatedFromUtc, query.CreatedToUtc);

        baseQuery = WhereReviewed(baseQuery, query);
        baseQuery = PostFilters.WhereHasReviews(baseQuery, query.HasReviews);

        var projectedQuery = PostFilters.WhereRated(baseQuery.Select(PostFilters.WithRating),
            query.MinRating, query.MaxRating);

        return SortRated(projectedQuery, query);
    }

    private IQueryable<DbPost> WhereReviewed(IQueryable<DbPost> baseQuery, PostsQuery query)
    {
        // Reviewer filter — keep only posts which have at least one active
        // review authored by the given username. Powers the profile page
        // The "Оценил чужих постов: {username}" page. Subquery against PostReviews
        // mirrors the LastReviewedFromUtc pattern below for plan stability.
        if (!string.IsNullOrWhiteSpace(query.ReviewerUsername))
        {
            var reviewer = query.ReviewerUsername.Trim();
            baseQuery = baseQuery.Where(p => _dbContext.PostReviews
                .Any(r => r.PostId == p.PostId &&
                          !r.IsRemoved &&
                          !r.Author.IsRemoved &&
                          r.Author.Username == reviewer));
        }

        // LastReviewedFromUtc filter — only posts that have a review at or after this date
        if (query.LastReviewedFromUtc.HasValue)
        {
            var after = query.LastReviewedFromUtc.Value;
            baseQuery = baseQuery.Where(p => _dbContext.PostReviews
                .Any(r => r.PostId == p.PostId &&
                          !r.IsRemoved &&
                          r.CreatedUtc >= after));
        }

        return baseQuery;
    }

    /// <summary>
    /// The order the feed asked for, by rating and newest first unless it says
    /// otherwise.
    /// </summary>
    private static IOrderedQueryable<PostWithRating> SortRated(
        IQueryable<PostWithRating> projectedQuery, PostsQuery query)
    {
        var sortBy = query.SortBy?.ToLower() ?? "rating";
        var desc = !string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "created" => desc
                ? projectedQuery.OrderByDescending(x => x.Post.CreatedUtc)
                : projectedQuery.OrderBy(x => x.Post.CreatedUtc),
            "lastreview" => desc
                ? projectedQuery.OrderByDescending(x => x.LastReviewUtc)
                : projectedQuery.OrderBy(x => x.LastReviewUtc),
            "reviewcount" => desc
                ? projectedQuery.OrderByDescending(x => x.ReviewCount).ThenByDescending(x => x.Post.CreatedUtc)
                : projectedQuery.OrderBy(x => x.ReviewCount).ThenBy(x => x.Post.CreatedUtc),
            _ => desc
                ? projectedQuery.OrderByDescending(x => x.Rating).ThenByDescending(x => x.Post.CreatedUtc)
                : projectedQuery.OrderBy(x => x.Rating).ThenBy(x => x.Post.CreatedUtc)
        };
    }

    private static Post ToRatedPost(RatedPostRow row)
    {
        var post = row.Post;
        post.Room = new RoomRef
        {
            Id = post.RoomId,
            RoomNumber = row.RoomNumber,
            Title = row.RoomTitle,
            // Game populated by AttachGames via batched GetByIds. Game scalars are
            // dropped in favor of that batched hydration, which returns the
            // full GameDto (see sidebar-pattern notes in GameModels.RoomRef).
            Game = null
        };
        post.AuthorGameRole = AuthorGameRoleOf(post);
        return post;
    }

    private async Task AttachGames(Post[] posts, Guid viewerId)
    {
        // Batched game hydration — fetch the full GameRef-tier payload
        // for every unique game id on the page and attach it to each
        // post's Room. This mirrors the sidebar's data-flow: the list
        // endpoint already returns fully-populated games; GameLink /
        // RoomLink can build tooltips directly off `post.room.game`
        // without the frontend making a second round-trip per game.
        // The cost is a single EnrichGamesAsync call (~10 batched
        // queries, O(1) in page size) versus N HTTP calls from the
        // browser — strict improvement.
        var uniqueGameIds = posts
            .Select(p => p.GameId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (uniqueGameIds.Length > 0)
        {
            // Two different ids on purpose. Accessibility is Guid.Empty because
            // this feed has already fixed its own visibility in GetRated — open rooms
            // of approved, non-draft games — and the real viewer could only
            // narrow that, dropping a game they cannot see out of the dictionary
            // and leaving a live post with a null Room.Game. The viewer-scoped
            // fields are filled for the real viewer, because otherwise a
            // subscriber loses "reader" from the attached game's participation:
            // the whole subscriber set used to travel in the DTO and the
            // resolver found them in it.
            //
            // The game's blacklist has no term here and none in any other post
            // read (GameAccessibilityFilters): it closes writing, not reading.
            var gamesById = (await _gameRepository.GetByIds(uniqueGameIds, Guid.Empty, viewerId))
                .ToDictionary(g => g.Id);

            foreach (var post in posts)
            {
                if (post.Room is null) continue;
                if (gamesById.TryGetValue(post.GameId, out var hydratedGame))
                {
                    post.Room.Game = hydratedGame;
                }
            }
        }
    }
}
