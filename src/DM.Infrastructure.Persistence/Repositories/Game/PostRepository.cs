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

    public Task<int> Count(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            .CountAsync();
    }

    public async Task<IEnumerable<Post>> Get(Guid roomId, PagingData paging, Guid userId)
    {
        var posts = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            // Ends on the identifier, because CreatedUtc is not unique: a busy second ties
            // on its own, and the import of DM2 carries whole rooms written under one
            // timestamp. Paging asks for the same order once per page, and rows the order
            // cannot tell apart may come back arranged differently between two of those
            // asks, which shows one post on both pages and another on neither. Comments
            // (CommentSorting) and messages (the ChatId composite) close their order the
            // same way; the index behind this one is (RoomId, CreatedUtc, PostId).
            .OrderBy(p => p.CreatedUtc)
            .ThenBy(p => p.PostId)
            .Page(paging)
            .ProjectToPost()
            .ToArrayAsync();

        await EnrichWithCharacterPictures(posts);
        await EnrichWithAttachments(posts);
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
        var baseQuery = _dbContext.Posts
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
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePatterns.Contains(query.Search);
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(
                EF.Property<string>(p, "SearchText"), pattern));
        }

        // Author filter (repeated parameter, materialised before LINQ)
        if (query.AuthorUsernames is { Count: > 0 })
        {
            var usernames = query.AuthorUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .ToArray();
            if (usernames.Length > 0)
            {
                baseQuery = baseQuery.Where(p => usernames.Contains(p.Author.Username));
            }
        }

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
                          r.Author.Username == reviewer));
        }

        // Post creation date filters
        if (query.CreatedFromUtc.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.CreatedUtc >= query.CreatedFromUtc.Value);
        }
        if (query.CreatedToUtc.HasValue)
        {
            baseQuery = baseQuery.WhereAtOrBefore(p => p.CreatedUtc, query.CreatedToUtc.Value);
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

        // Review stats via the Post.Reviews navigation property. EF Core
        // translates each aggregate into a correlated subquery, but the
        // subqueries share a single filter (!IsRemoved) so the query
        // planner can fuse them — net cost on a 20-row page is three
        // LEFT JOIN LATERALs, not 60 independent round-trips.
        //
        // History: a previous refactor tried to collapse these into a
        // GROUP BY subquery + LEFT JOIN + `s == null ? 0 : s.Rating`
        // ternary. That shape passed in hand-written scenarios but made
        // EF's CountAsync path fail translation ("could not be
        // translated" on the ternary-over-grouped-subquery expression),
        // which silently broke the rated-posts listing whenever it ran
        // against actual data. Navigation-property aggregates have been
        // EF's first-class story since 7.0 and translate reliably.
        var projectedQuery = baseQuery.Select(p => new PostWithRating
        {
            Post = p,
            Rating = p.Reviews.Where(r => !r.IsRemoved).Sum(r => (int?)r.SignValue) ?? 0,
            ReviewCount = p.Reviews.Count(r => !r.IsRemoved),
            LastReviewUtc = p.Reviews.Where(r => !r.IsRemoved).Max(r => (DateTimeOffset?)r.CreatedUtc)
        });

        // HasReviews filter
        if (query.HasReviews == true)
        {
            projectedQuery = projectedQuery.Where(x => x.ReviewCount > 0);
        }

        // MinRating filter
        if (query.MinRating.HasValue)
        {
            projectedQuery = projectedQuery.Where(x => x.Rating >= query.MinRating.Value);
        }

        // MaxRating filter (rating can be negative)
        if (query.MaxRating.HasValue)
        {
            projectedQuery = projectedQuery.Where(x => x.Rating <= query.MaxRating.Value);
        }

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
            // The feed's card does not show edit history, and the shared formula
            // reads it for the room view that does. Cleared rather than kept: what
            // a card shows is a decision of its own, not a side effect of the two
            // reads coming to share one projection.
            post.Edits = [];
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
            // narrow that, dropping a game they are blacklisted from out of the
            // dictionary and leaving a live post with a null Room.Game. The
            // viewer-scoped fields are filled for the real viewer, because
            // otherwise a subscriber loses "reader" from the attached game's
            // participation: the whole subscriber set used to travel in the DTO
            // and the resolver found them in it.
            //
            // Note what the accessibility choice does NOT do: it is not what
            // lets a blacklisted user read this feed. The feed's own filter has
            // no blacklist term at all, unlike every other post read, which goes
            // through GameAccessibilityFilters.RoomAvailable. Closing that is a
            // decision about what a blacklist means for reading, not a choice of
            // sentinel here.
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

            // The rolls of the post, in the same transaction (INV-6): a roll
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

        return await _dbContext.Posts
            .Where(p => p.PostId == createPost.PostId)
            .ProjectToPost()
            .FirstAsync();
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
    /// </remarks>
    public async Task Delete(Guid postId, Guid deletedByUserId)
    {
        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            // Read inside the block: the clear above drops the tracked post, so it
            // has to be loaded again. On the first attempt this costs nothing extra.
            var post = await _dbContext.Posts.FindAsync(postId);
            if (post == null)
            {
                return;
            }

            var authorId = post.AuthorId;
            var now = _dateTimeProvider.Now;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            SoftDelete.Mark(post, deletedByUserId, now);
            await _dbContext.SaveChangesAsync();

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
        });
    }

    #endregion
}
