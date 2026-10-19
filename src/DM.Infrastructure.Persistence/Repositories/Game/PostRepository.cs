using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Attachments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostEdit = DM.Infrastructure.Persistence.Entities.Game.Posts.PostEdit;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal partial class PostRepository : IPostRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGameRepository _gameRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// Page row of a room's read: the post exactly as every other read projects it.
    /// </summary>
    private static readonly Expression<Func<PostWithRating, Post>> RoomPostProjection =
        ExpressionSplicer.Expand<Func<PostWithRating, Post>>(
            x => PostMappers.PostProjection.Splice(x.Post));

    public PostRepository(
        DmDbContext dbContext,
        IGameRepository gameRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _gameRepository = gameRepository;
        _dateTimeProvider = dateTimeProvider;
    }

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

        posts = PostFilters.WhereWrittenBy(posts, query.Search, query.AuthorUsernames, authorIds,
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

        return PostFilters.WhereRated(posts.Select(PostFilters.WithRating), query.HasReviews, query.MinRating, query.MaxRating);
    }

    /// <summary>
    /// The order the reader asked for, closed on the writing order in the same
    /// direction.
    /// </summary>
    /// <remarks>
    /// Every field ends on (CreatedUtc, PostId): none is unique, and paging needs one order
    /// on every page. The index behind the writing order is (RoomId, CreatedUtc, PostId).
    /// </remarks>
    private static IOrderedQueryable<PostWithRating> SortRoomPosts(
        IQueryable<PostWithRating> posts, RoomPostsQuery query)
    {
        var ascending = !string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "rating" => posts.OrderByDirection(x => x.Rating, ascending)
                .ThenByDirection(x => x.Post.CreatedUtc, ascending),
            "reviewcount" => posts.OrderByDirection(x => x.ReviewCount, ascending)
                .ThenByDirection(x => x.Post.CreatedUtc, ascending),
            // A post nobody reviewed goes last either way: PostgreSQL puts NULL first descending.
            "lastreview" => posts.OrderBy(x => x.LastReviewUtc == null)
                .ThenByDirection(x => x.LastReviewUtc, ascending)
                .ThenByDirection(x => x.Post.CreatedUtc, ascending),
            _ => posts.OrderByDirection(x => x.Post.CreatedUtc, ascending)
        };

        return ordered.ThenByDirection(x => x.Post.PostId, ascending);
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
    private async Task EnrichWithNumbers(IReadOnlyCollection<Post> posts, CancellationToken ct = default)
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
            """).ToDictionaryAsync(row => row.PostId, row => row.Number, ct);

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

    /// <inheritdoc />
    /// <remarks>
    /// Reached through the rooms, the same way the paged read above is, so the
    /// reader's scope is applied by the one filter that expresses it. Asking
    /// Posts directly answered with the text of any post whose identifier the
    /// caller happened to know, private room or not: the userId argument was
    /// accepted and never used, which the compiler has no reason to mention.
    /// </remarks>
    public async Task<Post?> Get(Guid postId, Guid userId, CancellationToken ct = default)
    {
        var post = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.Posts)
            .Where(p => p.PostId == postId)
            .ProjectToPost()
            .FirstOrDefaultAsync(ct);

        if (post != null)
        {
            await EnrichWithCharacterPictures(new[] { post }, ct);
            await EnrichWithAttachments(new[] { post }, ct);
            await EnrichWithNumbers(new[] { post }, ct);
        }
        return post;
    }

    /// <summary>
    /// Batch-loads the files attached to all posts on the page and fills
    /// <see cref="Post.Attachments"/>, from one read of the attachment rows
    /// (<see cref="AttachmentRows.OfPostsAsync"/>).
    /// </summary>
    private async Task EnrichWithAttachments(IReadOnlyCollection<Post> posts, CancellationToken ct = default)
    {
        var byPost = await AttachmentRows.OfPostsAsync(_dbContext, posts.Select(p => p.Id).Distinct().ToList(), ct);

        foreach (var post in posts)
        {
            if (byPost.TryGetValue(post.Id, out var attachments))
            {
                post.Attachments = attachments;
            }
        }
    }

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

        // A save that round-trips the values the post holds leaves it Unchanged
        // and writes no line of history, as the edit of a topic does.
        if (_dbContext.Entry(post).State == EntityState.Modified)
        {
            _dbContext.PostEdits.Add(new DbPostEdit
            {
                PostEditId = updatePost.PostEditId,
                PostId = post.PostId,
                EditorUserId = updatePost.EditorUserId,
                ModifiedUtc = updatePost.EditedUtc
            });
        }

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
            var marked = await SoftDelete.MarkWhere(
                _dbContext.Posts.Where(p => p.PostId == postId && !p.IsRemoved), deletedByUserId, now);
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
            await AttachmentRows.RetireAsync(
                _dbContext.Uploads.Where(u => u.TargetPostId == postId && u.Type == UploadType.PostAttachment),
                deletedByUserId, now);

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
}
