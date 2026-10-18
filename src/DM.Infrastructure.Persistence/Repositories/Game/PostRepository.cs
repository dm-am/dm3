using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Helper class for rated posts projection
/// </summary>
internal class PostWithRating
{
    public required Entities.Game.Posts.Post Post { get; init; }
    public int Rating { get; init; }
    public int ReviewCount { get; init; }
    public DateTimeOffset? LastReviewUtc { get; init; }
}

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

/// <inheritdoc />
internal class PostRepository : IPostRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGameRepository _gameRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

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

    /// <summary>
    /// Page row of a room's read: the post exactly as every other read projects it.
    /// </summary>
    private static readonly Expression<Func<PostWithRating, Post>> RoomPostProjection =
        ExpressionSplicer.Expand<Func<PostWithRating, Post>>(
            x => PostMappers.PostProjection.Splice(x.Post));

    /// <summary>
    /// The review figures a list filters and sorts by, beside the post. EF Core
    /// translates each aggregate into a correlated subquery, but the subqueries share
    /// a single filter (!IsRemoved) so the query planner can fuse them — net cost on
    /// a 20-row page is three LEFT JOIN LATERALs, not 60 independent round-trips.
    /// </summary>
    /// <remarks>
    /// History: a previous refactor tried to collapse these into a GROUP BY subquery
    /// + LEFT JOIN + <c>s == null ? 0 : s.Rating</c> ternary. That shape passed in
    /// hand-written scenarios but made EF's CountAsync path fail translation ("could
    /// not be translated" on the ternary-over-grouped-subquery expression), which
    /// silently broke the rated-posts listing whenever it ran against actual data.
    /// Navigation-property aggregates have been EF's first-class story since 7.0 and
    /// translate reliably.
    /// </remarks>
    private static readonly Expression<Func<DbPost, PostWithRating>> WithRating = p => new PostWithRating
    {
        Post = p,
        Rating = p.Reviews.Where(r => !r.IsRemoved).Sum(r => (int?)r.SignValue) ?? 0,
        ReviewCount = p.Reviews.Count(r => !r.IsRemoved),
        LastReviewUtc = p.Reviews.Where(r => !r.IsRemoved).Max(r => (DateTimeOffset?)r.CreatedUtc)
    };

    /// <inheritdoc />
    public PostRepository(
        DmDbContext dbContext,
        IGameRepository gameRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _gameRepository = gameRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Read Operations

    public async Task<int> Count(Guid roomId, RoomPostsQuery query, Guid userId) =>
        await RoomPosts(roomId, query, userId,
            await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames)).CountAsync();

    public async Task<IEnumerable<Post>> Get(Guid roomId, RoomPostsQuery query, PagingData paging, Guid userId)
    {
        var authorIds = await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames);
        var posts = await SortRoomPosts(RoomPosts(roomId, query, userId, authorIds), query)
            .Page(paging)
            .Select(RoomPostProjection)
            .ToArrayAsync();

        if (IsTheRoomAsWritten(query))
        {
            // The page is a stretch of the room itself, so a post's place on it is
            // its place in the room. The one read that needs no numbering query is
            // also the one read nearly every visit makes.
            for (var i = 0; i < posts.Length; i++)
            {
                posts[i].Number = paging.Skip + i + 1;
            }
        }
        else
        {
            await EnrichWithNumbers(posts);
        }

        await EnrichWithCharacterPictures(posts);
        await EnrichWithAttachments(posts);
        return posts;
    }

    /// <summary>
    /// The posts of one room the reader may see and the query admits, each with its
    /// rating, in no particular order yet. Shared by the count and the page, so a
    /// filter reaches pagination and page alike.
    /// </summary>
    /// <remarks>
    /// Reached through the rooms, so the reader's scope is the one filter that
    /// expresses it: a room outside it holds no posts rather than refusing.
    ///
    /// Keeps the posts of removed accounts, signed by nobody
    /// (<see cref="RemovedAuthors"/>): the page projects through the post formula
    /// itself rather than through ProjectToPost, so it lifts the account filter
    /// on its own.
    /// </remarks>
    private IQueryable<PostWithRating> RoomPosts(
        Guid roomId, RoomPostsQuery query, Guid userId, IReadOnlyCollection<Guid>? authorIds)
    {
        var posts = _dbContext.Rooms
            .KeepingRemovedAuthors()
            .AsNoTracking()
            .TagWith("DM.Game.RoomPosts")
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts);

        posts = WhereWrittenBy(posts, query.Search, query.AuthorUsernames, authorIds,
            query.CreatedFromUtc, query.CreatedToUtc);

        // The characters and "without a character" are one choice: a post matches
        // when it is by any chosen character or, with the flag, by none.
        var characterIds = query.CharacterIds?.Distinct().ToArray() ?? [];
        var withoutCharacter = query.WithoutCharacter == true;
        if (characterIds.Length > 0)
        {
            posts = withoutCharacter
                ? posts.Where(p => p.CharacterId == null || characterIds.Contains(p.CharacterId.Value))
                : posts.Where(p => p.CharacterId != null && characterIds.Contains(p.CharacterId.Value));
        }
        else if (withoutCharacter)
        {
            posts = posts.Where(p => p.CharacterId == null);
        }

        return WhereRated(posts.Select(WithRating), query.HasReviews, query.MinRating, query.MaxRating);
    }

    /// <summary>
    /// The order the reader asked for, closed on the writing order in the same
    /// direction.
    /// </summary>
    /// <remarks>
    /// Every field ends on (CreatedUtc, PostId), because none of them is unique:
    /// ratings and review counts tie by design, a busy second gives several posts one
    /// timestamp, and the import of DM2 carries whole rooms written under one. Paging
    /// asks for the same order once per page, and rows the order cannot tell apart
    /// may come back arranged differently between two of those asks, which shows one
    /// post on both pages and another on neither. The index behind the writing order
    /// is (RoomId, CreatedUtc, PostId).
    /// </remarks>
    private static IOrderedQueryable<PostWithRating> SortRoomPosts(
        IQueryable<PostWithRating> posts, RoomPostsQuery query)
    {
        var desc = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<PostWithRating>? ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "rating" => desc
                ? posts.OrderByDescending(x => x.Rating)
                : posts.OrderBy(x => x.Rating),
            "reviewcount" => desc
                ? posts.OrderByDescending(x => x.ReviewCount)
                : posts.OrderBy(x => x.ReviewCount),
            // A post nobody reviewed has no last review, and PostgreSQL puts NULL
            // first in a descending order: "the latest reviewed" would open on every
            // post without a review. Those go last in either direction.
            "lastreview" => desc
                ? posts.OrderBy(x => x.LastReviewUtc == null).ThenByDescending(x => x.LastReviewUtc)
                : posts.OrderBy(x => x.LastReviewUtc == null).ThenBy(x => x.LastReviewUtc),
            _ => null
        };

        if (ordered == null)
        {
            return desc
                ? posts.OrderByDescending(x => x.Post.CreatedUtc).ThenByDescending(x => x.Post.PostId)
                : posts.OrderBy(x => x.Post.CreatedUtc).ThenBy(x => x.Post.PostId);
        }

        return desc
            ? ordered.ThenByDescending(x => x.Post.CreatedUtc).ThenByDescending(x => x.Post.PostId)
            : ordered.ThenBy(x => x.Post.CreatedUtc).ThenBy(x => x.Post.PostId);
    }

    /// <summary>
    /// Whether the query asks for the room itself: every post, in the order it was
    /// written.
    /// </summary>
    private static bool IsTheRoomAsWritten(RoomPostsQuery query) =>
        string.IsNullOrWhiteSpace(query.Search) &&
        (query.AuthorUsernames == null || query.AuthorUsernames.All(string.IsNullOrWhiteSpace)) &&
        query.CharacterIds is not { Count: > 0 } &&
        query.WithoutCharacter != true &&
        query.MinRating == null &&
        query.MaxRating == null &&
        query.HasReviews == null &&
        query.CreatedFromUtc == null &&
        query.CreatedToUtc == null &&
        (string.IsNullOrEmpty(query.SortBy) ||
         string.Equals(query.SortBy, "created", StringComparison.OrdinalIgnoreCase)) &&
        !string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fill <see cref="Post.Number"/>: the post's place in its room, in the order the
    /// room is written in, whatever order the posts were read in.
    /// </summary>
    /// <remarks>
    /// One statement for the page, numbering every live post of its rooms once. A
    /// count per post ("how many were written before this one") costs the post's
    /// place in the room, and a page sorted by rating is scattered across the whole
    /// room, so twenty of them add up to ten passes over it rather than one.
    ///
    /// Written in SQL because the numbering is a window function, which LINQ does
    /// not express. The soft-delete predicate is spelled out for that reason: the
    /// global query filter does not reach a raw statement, and a removed post has no
    /// place in the room. The order is the one the room is paged in, (CreatedUtc,
    /// PostId), so the number of a post is where the unfiltered room shows it.
    /// </remarks>
    private async Task EnrichWithNumbers(IReadOnlyCollection<Post> posts)
    {
        if (posts.Count == 0) return;

        var roomIds = posts.Select(p => p.RoomId).Distinct().ToArray();
        var postIds = posts.Select(p => p.Id).ToArray();

        var numbers = await _dbContext.Database.SqlQuery<PostNumberRow>($"""
            SELECT numbered."PostId", numbered."Number"
            FROM (
                SELECT p."PostId",
                       CAST(row_number() OVER (
                           PARTITION BY p."RoomId" ORDER BY p."CreatedUtc", p."PostId") AS integer) AS "Number"
                FROM "Posts" p
                WHERE p."RoomId" = ANY({roomIds}) AND NOT p."IsRemoved"
            ) AS numbered
            WHERE numbered."PostId" = ANY({postIds})
            """).ToDictionaryAsync(row => row.PostId, row => row.Number);

        foreach (var post in posts)
        {
            post.Number = numbers.TryGetValue(post.Id, out var number) ? number : null;
        }
    }

    /// <summary>One row of <see cref="EnrichWithNumbers"/>.</summary>
    private sealed class PostNumberRow
    {
        public Guid PostId { get; init; }
        public int Number { get; init; }
    }

    /// <summary>
    /// The filters on what a post says, who wrote it and when: the same on a room's
    /// list and on the rated feed, so one name means one thing on both.
    /// </summary>
    /// <remarks>
    /// <paramref name="authorIds" /> is what the names mean when "Аккаунт удален" is
    /// among them - the removed accounts and the live ones named alongside
    /// (<see cref="RemovedAccountScope" />) - and null otherwise.
    /// </remarks>
    private static IQueryable<DbPost> WhereWrittenBy(
        IQueryable<DbPost> posts,
        string? search,
        IReadOnlyCollection<string>? authorUsernames,
        IReadOnlyCollection<Guid>? authorIds,
        DateTimeOffset? createdFromUtc,
        DateTimeOffset? createdToUtc)
    {
        // Search filter (case-insensitive contains on the visible text of the post —
        // the projection written beside the body, which both full-text searches
        // already read).
        //
        // It used to match against GameText with the [private] block cut out by a
        // regexp inside the query, and the cut is what made the filter an oracle.
        // Cutting leaves a trace: the block becomes a space, and the whitespace
        // around it stays, so a phrase reaching across the place where the block
        // stood matches the post without it and misses the post with it. The reader
        // never sees a word of the hidden text and still learns it is there, from
        // which rows came back and which did not.
        //
        // The projection has nothing to cut. It is the plain-text render of the
        // body, where [private] is filtered out as a node rather than deleted as a
        // string, and the runs of whitespace it leaves are collapsed — so a post
        // carrying a hidden block and the same post without one project to the same
        // text, and this filter cannot tell them apart. It is also the string the
        // reader is shown: the markup is gone from it, so what matches here is what
        // is on the page.
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
    private static IQueryable<PostWithRating> WhereRated(
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

    /// <inheritdoc />
    /// <remarks>
    /// Reached through the rooms, the same way the paged read above is, so the
    /// reader's scope is applied by the one filter that expresses it. Asking
    /// Posts directly answered with the text of any post whose identifier the
    /// caller happened to know, private room or not: the userId argument was
    /// accepted and never used, which the compiler has no reason to mention.
    /// </remarks>
    public async Task<Post?> Get(Guid postId, Guid userId)
    {
        var post = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.Posts)
            .Where(p => p.PostId == postId)
            .ProjectToPost()
            .FirstOrDefaultAsync();

        if (post != null)
        {
            await EnrichWithCharacterPictures(new[] { post });
            await EnrichWithAttachments(new[] { post });
            await EnrichWithNumbers(new[] { post });
        }
        return post;
    }

    public async Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query, Guid viewerId)
    {
        // Read-only path feeding the home-page widgets (best of week, latest
        // featured, Pulse). PostWithRating below holds an entity reference, but no
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

        baseQuery = WhereWrittenBy(baseQuery, query.Search, query.AuthorUsernames,
            await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames),
            query.CreatedFromUtc, query.CreatedToUtc);

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

        var projectedQuery = WhereRated(baseQuery.Select(WithRating),
            query.HasReviews, query.MinRating, query.MaxRating);

        // Sorting
        var sortBy = query.SortBy?.ToLower() ?? "rating";
        var desc = !string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        var sortedQuery = sortBy switch
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

        var totalCount = await sortedQuery.CountAsync();

        var rawData = await sortedQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(RatedPostRowProjection)
            .ToArrayAsync();

        var posts = rawData.Select(row =>
        {
            var post = row.Post;
            post.Room = new RoomRef
            {
                Id = post.RoomId,
                RoomNumber = row.RoomNumber,
                Title = row.RoomTitle,
                // Game populated below via batched GetByIds. Game scalars are
                // dropped in favor of that batched hydration, which returns the
                // full GameDto (see sidebar-pattern notes in GameModels.RoomRef).
                Game = null
            };
            post.AuthorGameRole = AuthorGameRoleOf(post);
            return post;
        }).ToArray();

        // One batched query (IN(characterIds)) for avatar URLs, instead
        // of a correlated Uploads subquery per row in the main projection.
        await EnrichWithCharacterPictures(posts);
        await EnrichWithAttachments(posts);

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
            // this feed has already fixed its own visibility above — open rooms
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

        return (posts, totalCount);
    }

    /// <summary>
    /// The author's standing in the game the post belongs to, for the badge the
    /// rated feed puts on a post nobody's character signed.
    /// </summary>
    /// <remarks>
    /// A post with a character is a character's post and carries no badge — the
    /// name shown is the character's. Read off the post itself now that the
    /// projection fills the master and the assistants; it used to be two more
    /// members of the hand-written row.
    /// </remarks>
    private static string? AuthorGameRoleOf(Post post)
    {
        if (post.Character != null) return null;
        if (AnonymousIdentity.Is(post.AuthorUserId)) return null;
        if (post.AuthorUserId == post.GameMasterUserId) return "DungeonMaster";
        return post.GameAssistantUserIds.Contains(post.AuthorUserId) ? "Assistant" : null;
    }

    /// <summary>
    /// Batch-loads avatars of all characters on the page from the Uploads table
    /// and fills <see cref="CharacterShort.Picture"/> (3 URLs: original / medium /
    /// small). One query instead of N correlated subqueries.
    /// </summary>
    private async Task EnrichWithCharacterPictures(IEnumerable<Post> posts)
    {
        var characterIds = posts
            .Where(p => p.Character != null && p.Character.Id != Guid.Empty)
            .Select(p => p.Character!.Id)
            .Distinct()
            .ToList();

        if (characterIds.Count == 0) return;

        // Newest per character rather than a dictionary keyed on the target.
        // Nothing in the schema stops a second live row from pointing at the same
        // character, and ToDictionaryAsync answered a duplicate key by throwing —
        // which turned every read of the room into a 500 for everyone in it, with
        // no way back that did not involve editing rows by hand. The upload path
        // retires the previous portrait now, so a duplicate should not arise; the
        // read no longer depends on that being true.
        var rows = await _dbContext.Uploads
            .Where(u => u.TargetCharacterId != null
                && characterIds.Contains(u.TargetCharacterId.Value)
                && u.Type == UploadType.CharacterAvatar
                && !u.IsRemoved)
            .OrderByDescending(u => u.CreatedUtc)
            .ThenByDescending(u => u.UploadId)
            .Select(u => new
            {
                CharacterId = u.TargetCharacterId!.Value,
                Picture = Shared.Users.AvatarProjections.From(u),
            })
            .ToListAsync();

        var pictures = rows
            .GroupBy(x => x.CharacterId)
            .ToDictionary(g => g.Key, g => g.First().Picture);

        foreach (var post in posts)
        {
            if (post.Character != null && pictures.TryGetValue(post.Character.Id, out var picture))
            {
                post.Character.Picture = picture;
            }
        }
    }

    /// <summary>
    /// Batch-loads the files attached to all posts on the page and fills
    /// <see cref="Post.Attachments"/>.
    /// </summary>
    /// <remarks>
    /// One IN-query for the page, for the same reason EnrichWithCharacterPictures
    /// is one: a post may carry several attachments, so an inline collection
    /// projection would be a correlated subquery per row, and a room lists twenty
    /// rows at a time.
    ///
    /// The object key is not selected. It is the address of the bytes in a bucket
    /// whose post prefix answers nobody anonymously, and the only reason to have
    /// it here would be to put it somewhere a reader can see.
    /// </remarks>
    private async Task EnrichWithAttachments(IReadOnlyCollection<Post> posts)
    {
        if (posts.Count == 0) return;

        var postIds = posts.Select(p => p.Id).Distinct().ToList();

        var rows = await _dbContext.Uploads
            .Where(u => u.TargetPostId != null
                && postIds.Contains(u.TargetPostId.Value)
                && u.Type == UploadType.PostAttachment)
            .OrderBy(u => u.CreatedUtc)
            .ThenBy(u => u.UploadId)
            .Select(u => new
            {
                PostId = u.TargetPostId!.Value,
                Attachment = new PostAttachment
                {
                    Id = u.UploadId,
                    FileName = u.FileName ?? string.Empty,
                    ContentType = u.ContentType ?? string.Empty,
                    SizeBytes = u.SizeBytes,
                    Width = u.Width,
                    Height = u.Height,
                    CreatedUtc = u.CreatedUtc,
                },
            })
            .ToListAsync();

        if (rows.Count == 0) return;

        var byPost = rows
            .GroupBy(x => x.PostId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<PostAttachment>)
                g.Select(x => x.Attachment).ToList());

        foreach (var post in posts)
        {
            if (byPost.TryGetValue(post.Id, out var attachments))
            {
                post.Attachments = attachments;
            }
        }
    }

    #endregion

    #region Write Operations

    public async Task<Post> Create(CreatePostEntity createPost)
    {
        var dbPost = new DbPost
        {
            PostId = createPost.PostId,
            RoomId = createPost.RoomId,
            CharacterId = createPost.CharacterId,
            AuthorId = createPost.AuthorId,
            CreatedUtc = createPost.CreatedUtc,
            GameText = createPost.GameText,
            MetagameText = createPost.MetagameText,
            PrivateAddresseeSnapshotJson = createPost.PrivateAddresseeSnapshotJson,
            IsRemoved = false
        };
        // ExecuteUpdate runs and commits immediately while Add is deferred to
        // SaveChanges, so without a transaction a failure between them left the
        // author's QuantityRating incremented and the game's activity stamp moved
        // for a post that does not exist. QuantityRating feeds the user rating and
        // the stored IsNewbie column, so that drift is user-visible and nothing
        // recomputes it. The strategy wrapper is required because the API host
        // configures EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            _dbContext.Posts.Add(dbPost);

            // The rolls of the post, in the same transaction: a roll
            // cannot be produced a second time, so it must never exist without
            // the post or the post without it. EF orders the inserts under the
            // FK, so a single SaveChanges writes the post first.
            if (createPost.DiceRolls.Count > 0)
            {
                _dbContext.DiceRolls.AddRange(DiceRollRepository.MapToDb(createPost.DiceRolls));
            }

            await _dbContext.SaveChangesAsync();

            // Increment author's post count (QuantityRating)
            await _dbContext.Users
                .Where(u => u.UserId == createPost.AuthorId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating + 1));

            // Update game's LastPostCreatedUtc and reset inactivity warning if any
            var room = await _dbContext.Rooms.FindAsync(createPost.RoomId);
            if (room != null)
            {
                await _dbContext.Games
                    .Where(g => g.GameId == room.GameId)
                    .ExecuteUpdateAsync(g => g
                        .SetProperty(x => x.LastPostCreatedUtc, createPost.CreatedUtc)
                        .SetProperty(x => x.InactivityWarningUtc, (DateTimeOffset?)null));
            }

            await transaction.CommitAsync();
        });

        var created = await _dbContext.Posts
            .Where(p => p.PostId == createPost.PostId)
            .ProjectToPost()
            .FirstAsync();

        // The client puts the answer into the room it is reading, and a post
        // without its number there reads as a post outside the count.
        await EnrichWithNumbers(new[] { created });
        return created;
    }

    public async Task<Post?> Update(UpdatePostEntity updatePost)
    {
        var post = await _dbContext.Posts.FindAsync(updatePost.PostId);
        if (post == null)
            return null;

        // Update text always
        post.GameText = updatePost.GameText;
        post.MetagameText = updatePost.MetagameText;
        // Travels with the text it describes: a snapshot left behind by an edit
        // points at blocks the post no longer has.
        post.PrivateAddresseeSnapshotJson = updatePost.PrivateAddresseeSnapshotJson;

        // Update character if requested
        if (updatePost.ShouldChangeCharacter)
            post.CharacterId = updatePost.CharacterId;

        await _dbContext.SaveChangesAsync();

        var updated = await _dbContext.Posts
            .Where(p => p.PostId == updatePost.PostId)
            .ProjectToPost()
            .FirstOrDefaultAsync();

        // Enriched like both read paths: an edit answers with the post as it now
        // is, and a post that answers with no files after an edit is a payload
        // the block would draw empty the moment anything read it back.
        if (updated != null)
        {
            await EnrichWithAttachments(new[] { updated });
            await EnrichWithNumbers(new[] { updated });
        }

        return updated;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same transaction as Create, with the opposite sign. The removal and
    /// the author's QuantityRating were two separate commits, so a failure
    /// between them left a deleted post still counted or a live post uncounted —
    /// and QuantityRating feeds the user rating and the stored IsNewbie column,
    /// neither of which anything recomputes.
    ///
    /// The post's attachments go with it, in the same transaction: their rows are
    /// soft-deleted here, which starts the grace period the existing orphan
    /// sweeper waits out before dropping the objects. Nothing else would ever
    /// remove them — the sweeper walks soft-deleted upload rows, and a live row
    /// pointing at a hidden post is not one — so a deleted post used to leave its
    /// files in the bucket for the life of the bucket.
    ///
    /// The mark itself is the gate. The post used to be read, checked and marked
    /// over three steps with nothing holding the row in between, so two removals
    /// of one post both found it live and both took the author's count down: a
    /// stored column, lowered twice for one post, with nothing that recomputes it
    /// and no row left to say what the right number was. Here the live state is in
    /// the predicate of the write, and how many rows it matched decides whether
    /// anything else happens at all.
    /// </remarks>
    /// <returns>Whether this call is the one that removed the post</returns>
    public async Task<bool> Delete(Guid postId, Guid deletedByUserId)
    {
        var removed = false;

        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            // A replay decides again on what it finds, rather than carrying the
            // answer of the attempt whose transaction rolled back.
            removed = false;

            var now = _dateTimeProvider.Now;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // One statement reads and writes the row: a second removal arriving
            // together with this one waits on the row lock and then re-tests the
            // predicate against the committed state, so exactly one of the two
            // matches. Everything below hangs off that.
            var marked = await _dbContext.Posts
                .Where(p => p.PostId == postId && !p.IsRemoved)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)now));
            if (marked == 0)
            {
                return;
            }

            // After the mark, so the row it reads is the one this transaction now
            // holds; IgnoreQueryFilters, because that row is removed and the
            // global filter hides it.
            var authorId = await _dbContext.Posts
                .IgnoreQueryFilters()
                .TagWith("DM.Game.RemovedPostAuthor")
                .Where(p => p.PostId == postId)
                .Select(p => p.AuthorId)
                .FirstAsync();

            // The soft-delete filter on Uploads keeps this to the live rows, so a
            // file the author had already taken off does not get its grace period
            // restarted. The author of the deletion travels with it for the same
            // reason it does everywhere else: moderation has to be able to answer
            // who removed a file.
            await _dbContext.Uploads
                .Where(u => u.TargetPostId == postId && u.Type == UploadType.PostAttachment)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)now));

            // IgnoreQueryFilters, because the row this has to reach may be soft
            // deleted. The counter was raised when the post was written and has to
            // come back down when it is removed, and moderation removes the posts
            // of deactivated accounts as a matter of course - so without this the
            // update matched no rows, silently, and the count kept a post that no
            // longer exists. Nothing recomputes it afterwards: it is a column, not
            // a view, and the only trace of the drift is a number on a profile
            // nobody can reconcile against the posts.
            await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(u => u.UserId == authorId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating - 1));

            await transaction.CommitAsync();
            removed = true;
        });

        return removed;
    }

    #endregion
}
