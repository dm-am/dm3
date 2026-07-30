using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

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

/// <inheritdoc />
internal class PostRepository : IPostRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGameRepository _gameRepository;

    /// <inheritdoc />
    public PostRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGameRepository gameRepository)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _gameRepository = gameRepository;
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
            .OrderBy(p => p.CreatedUtc)
            .Page(paging)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await EnrichWithCharacterPictures(posts);
        return posts;
    }

    public async Task<Post?> Get(Guid postId, Guid userId)
    {
        var post = await _dbContext.Posts
            .Where(p => p.PostId == postId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (post != null)
        {
            await EnrichWithCharacterPictures(new[] { post });
        }
        return post;
    }

    public async Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query, Guid viewerId)
    {
        // Read-only query path feeding the home-page widgets (best of week,
        // latest featured, Pulse). AsNoTracking drops EF Core's change
        // tracker — we never intend to mutate these entities, and the
        // rich projection below holds a full Post reference which would
        // otherwise be tracked. See PERFORMANCE.md → "AsNoTracking".
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

        // Search filter (case-insensitive contains on GameText, excluding [private] blocks).
        // Uses PostgreSQL regexp_replace via DbFunction mapping to strip [private=X]...[/private]
        // before matching, so private text is never included in search results.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(
                DmDbContext.RegexpReplace(
                    p.GameText,
                    @"\[private(=[^\]]*)?\][\s\S]*?\[/private\]",
                    "",
                    "gi"),
                pattern));
        }

        // Author filter (comma-separated usernames, parsed before LINQ)
        if (!string.IsNullOrWhiteSpace(query.AuthorUsernames))
        {
            var usernames = query.AuthorUsernames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            baseQuery = baseQuery.Where(p => usernames.Contains(p.Author.Username));
        }

        // Reviewer filter — keep only posts which have at least one active
        // review authored by the given username. Powers the profile page
        // The "Оценил чужих постов: {username}" page. Subquery against PostReviews
        // mirrors the LastReviewedAfter pattern below for plan stability.
        if (!string.IsNullOrWhiteSpace(query.ReviewerUsername))
        {
            var reviewer = query.ReviewerUsername.Trim();
            baseQuery = baseQuery.Where(p => _dbContext.PostReviews
                .Any(r => r.PostId == p.PostId &&
                          !r.IsRemoved &&
                          r.Author.Username == reviewer));
        }

        // Post creation date filters
        if (query.CreatedAfter.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.CreatedUtc >= query.CreatedAfter.Value);
        }
        if (query.CreatedBefore.HasValue)
        {
            baseQuery = baseQuery.WhereAtOrBefore(p => p.CreatedUtc, query.CreatedBefore.Value);
        }

        // LastReviewedAfter filter — only posts that have a review after this date
        if (query.LastReviewedAfter.HasValue)
        {
            var after = query.LastReviewedAfter.Value;
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

        // Project to anonymous type first to avoid EF Core issues with complex conditionals
        var rawData = await sortedQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(x => new
            {
                x.Post.PostId,
                x.Post.RoomId,
                x.Post.GameText,
                MetagameText = x.Post.MetagameText ?? string.Empty,
                x.Post.CreatedUtc,
                x.Rating,
                x.ReviewCount,
                // Author
                AuthorUserId = x.Post.Author.UserId,
                AuthorUsername = x.Post.Author.Username,
                AuthorRole = x.Post.Author.Role,
                AuthorStatus = x.Post.Author.Status,
                // Game master/assistant info for author role
                GameMasterId = x.Post.Room.Game.MasterId,
                IsAuthorAssistant = x.Post.Room.Game.Assistants.Any(a => a.UserId == x.Post.Author.UserId),
                // Room — game scalars dropped in favor of a batched
                // hydration step below that returns the full GameDto
                // (see sidebar-pattern notes in GameModels.RoomRef).
                RoomNumber = x.Post.Room.RoomNumber,
                RoomTitle = x.Post.Room.Title,
                GameId = x.Post.Room.Game.GameId,
                // Character — nullable navigation; `!` tells the compiler
                // the subsequent member access is intentional. At SQL
                // generation time EF Core emits LEFT JOINs that null-
                // propagate cleanly, and the post-pagination projection
                // below gates on CharId.HasValue before touching any of
                // these fields, so a null character never reaches
                // CharacterShort construction.
                CharId = (Guid?)x.Post.Character!.CharacterId,
                CharName = x.Post.Character!.Name,
                CharAuthorUserId = (Guid?)x.Post.Character!.Author!.UserId,
                CharAuthorUsername = x.Post.Character!.Author!.Username,
                CharAuthorRole = (UserRole?)x.Post.Character!.Author!.Role,
                CharAuthorStatus = x.Post.Character!.Author!.Status,
                CharIsNpc = (bool?)x.Post.Character!.IsNpc
                // Character picture is resolved post-pagination via
                // EnrichWithCharacterPictures — one batched IN-query per
                // page instead of an inline correlated subquery per row.
            })
            .ToArrayAsync();

        // Map to domain objects in memory with proper null handling
        var posts = rawData.Select(x =>
        {
            // Character is only valid if all required fields are present
            CharacterShort? character = null;
            if (x.CharId.HasValue && x.CharAuthorUserId.HasValue && x.CharAuthorRole.HasValue)
            {
                character = new CharacterShort
                {
                    Id = x.CharId.Value,
                    Name = x.CharName ?? string.Empty,
                    // Picture is filled in batch by EnrichWithCharacterPictures.
                    IsNpc = x.CharIsNpc ?? false,
                    Author = new GeneralUser
                    {
                        UserId = x.CharAuthorUserId.Value,
                        Username = x.CharAuthorUsername ?? string.Empty,
                        Role = x.CharAuthorRole.Value,
                        Status = x.CharAuthorStatus
                    }
                };
            }

            // Determine author's game role (DungeonMaster/Assistant/null)
            string? authorGameRole = null;
            if (character == null) // Post without character = master post
            {
                if (x.AuthorUserId == x.GameMasterId)
                    authorGameRole = "DungeonMaster";
                else if (x.IsAuthorAssistant)
                    authorGameRole = "Assistant";
            }

            return new Post
            {
                Id = x.PostId,
                RoomId = x.RoomId,
                GameText = x.GameText,
                MetagameText = x.MetagameText,
                CreatedUtc = x.CreatedUtc,
                Rating = x.Rating,
                ReviewCount = x.ReviewCount,
                AuthorGameRole = authorGameRole,
                Author = new GeneralUser
                {
                    UserId = x.AuthorUserId,
                    Username = x.AuthorUsername,
                    Role = x.AuthorRole,
                    Status = x.AuthorStatus
                },
                Room = new RoomRef
                {
                    Id = x.RoomId,
                    RoomNumber = x.RoomNumber,
                    Title = x.RoomTitle,
                    // Game populated below via batched GetByIds.
                    Game = null
                },
                Character = character!
            };
        }).ToArray();

        // One batched query (IN(characterIds)) for avatar URLs, instead
        // of a correlated Uploads subquery per row in the main projection.
        await EnrichWithCharacterPictures(posts);

        // Batched game hydration — fetch the full GameRef-tier payload
        // for every unique game id on the page and attach it to each
        // post's Room. This mirrors the sidebar's data-flow: the list
        // endpoint already returns fully-populated games; GameLink /
        // RoomLink can build tooltips directly off `post.room.game`
        // without the frontend making a second round-trip per game.
        // The cost is a single EnrichGamesAsync call (~10 batched
        // queries, O(1) in page size) versus N HTTP calls from the
        // browser — strict improvement.
        var uniqueGameIds = rawData
            .Select(x => x.GameId)
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

            var rawByPostId = rawData.ToDictionary(x => x.PostId);
            foreach (var post in posts)
            {
                if (post.Room is null) continue;
                if (!rawByPostId.TryGetValue(post.Id, out var raw)) continue;
                if (gamesById.TryGetValue(raw.GameId, out var hydratedGame))
                {
                    post.Room.Game = hydratedGame;
                }
            }
        }

        return (posts, totalCount);
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
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempted = false;
        await strategy.ExecuteAsync(async () =>
        {
            if (attempted)
            {
                // A retry replays this block; the post the failed attempt left
                // tracked would otherwise be inserted twice or not at all.
                _dbContext.ChangeTracker.Clear();
            }

            attempted = true;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            _dbContext.Posts.Add(dbPost);
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
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
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

        // Handle soft delete if requested
        if (updatePost.IsRemoved.HasValue)
            post.IsRemoved = updatePost.IsRemoved.Value;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Posts
            .Where(p => p.PostId == updatePost.PostId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task Delete(Guid postId)
    {
        var post = await _dbContext.Posts.FindAsync(postId);
        if (post != null)
        {
            post.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task DecrementAuthorQuantityRating(Guid authorId)
    {
        await _dbContext.Users
            .Where(u => u.UserId == authorId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating - 1));
    }

    #endregion
}
