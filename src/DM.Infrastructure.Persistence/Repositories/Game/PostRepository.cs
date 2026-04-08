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

    /// <inheritdoc />
    public PostRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
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

    public async Task<BestPostResult?> GetBestPost(Guid userId)
    {
        var result = await _dbContext.Posts
            .Where(p => p.AuthorId == userId)
            .Where(p => p.Room.AccessType == RoomAccessType.Open)
            .Select(p => new
            {
                Post = p,
                Reviews = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved)
            })
            .Select(x => new
            {
                x.Post,
                Rating = x.Reviews.Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = x.Reviews.Count()
            })
            .Where(x => x.Rating > 0)
            .OrderByDescending(x => x.Rating)
            .Select(x => new BestPostResult
            {
                PostId = x.Post.PostId,
                GameText = x.Post.GameText,
                GameTitle = x.Post.Room.Game.Title,
                GameId = x.Post.Room.Game.GameId,
                RoomTitle = x.Post.Room.Title,
                RoomId = x.Post.RoomId,
                AuthorUsername = x.Post.Author.Username,
                Rating = x.Rating,
                ReviewCount = x.ReviewCount,
                CreatedUtc = x.Post.CreatedUtc
            })
            .FirstOrDefaultAsync();
        return result;
    }

    public async Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query)
    {
        var baseQuery = _dbContext.Posts
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

        // Search filter (case-insensitive contains on GameText)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.GameText, pattern));
        }

        // ReviewedAfter filter
        if (query.ReviewedAfter.HasValue)
        {
            var after = query.ReviewedAfter.Value;
            baseQuery = baseQuery.Where(p => _dbContext.PostReviews
                .Any(r => r.PostId == p.PostId &&
                          !r.IsRemoved &&
                          r.CreatedUtc >= after));
        }

        // Project with rating info
        // When ReviewedAfter is set, calculate rating only from reviews in that period
        IQueryable<PostWithRating> projectedQuery;
        if (query.ReviewedAfter.HasValue)
        {
            var after = query.ReviewedAfter.Value;
            projectedQuery = baseQuery.Select(p => new PostWithRating
            {
                Post = p,
                Rating = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved && r.CreatedUtc >= after)
                    .Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved && r.CreatedUtc >= after)
                    .Count(),
                LastReviewUtc = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved && r.CreatedUtc >= after)
                    .Max(r => (DateTimeOffset?)r.CreatedUtc)
            });
        }
        else
        {
            projectedQuery = baseQuery.Select(p => new PostWithRating
            {
                Post = p,
                Rating = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved)
                    .Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved)
                    .Count(),
                LastReviewUtc = _dbContext.PostReviews
                    .Where(r => r.PostId == p.PostId && !r.IsRemoved)
                    .Max(r => (DateTimeOffset?)r.CreatedUtc)
            });
        }

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
                // Room
                RoomNumber = x.Post.Room.RoomNumber,
                RoomTitle = x.Post.Room.Title,
                GameId = x.Post.Room.Game.GameId,
                GamePublicId = x.Post.Room.Game.PublicId,
                GameTitle = x.Post.Room.Game.Title,
                // Character - use navigation property check
                CharId = (Guid?)x.Post.Character!.CharacterId,
                CharName = x.Post.Character!.Name,
                CharAuthorUserId = (Guid?)x.Post.Character!.Author.UserId,
                CharAuthorUsername = x.Post.Character!.Author.Username,
                CharAuthorRole = (UserRole?)x.Post.Character!.Author.Role,
                CharAuthorStatus = x.Post.Character!.Author.Status,
                CharIsNpc = (bool?)x.Post.Character!.IsNpc,
                // Character picture - join with Uploads table
                CharPictureUrl = _dbContext.Uploads
                    .Where(u => u.EntityId == x.Post.Character!.CharacterId && u.Type == UploadType.CharacterAvatar)
                    .Select(u => u.MediumFilePath ?? u.FilePath)
                    .FirstOrDefault()
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
                    PictureUrl = x.CharPictureUrl ?? string.Empty,
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
                    GameId = x.GameId,
                    GamePublicId = x.GamePublicId,
                    GameTitle = x.GameTitle
                },
                Character = character!
            };
        }).ToArray();

        return (posts, totalCount);
    }

    /// <summary>
    /// Enriches posts with character picture URLs from the uploads table
    /// </summary>
    private async Task EnrichWithCharacterPictures(IEnumerable<Post> posts)
    {
        var characterIds = posts
            .Where(p => p.Character != null && p.Character.Id != Guid.Empty)
            .Select(p => p.Character!.Id)
            .Distinct()
            .ToList();

        if (characterIds.Count == 0) return;

        var pictureUrls = await _dbContext.Uploads
            .Where(u => u.EntityId != null && characterIds.Contains(u.EntityId.Value) && u.Type == UploadType.CharacterAvatar)
            .ToDictionaryAsync(
                u => u.EntityId!.Value,
                u => u.MediumFilePath ?? u.FilePath);

        foreach (var post in posts)
        {
            if (post.Character != null && pictureUrls.TryGetValue(post.Character.Id, out var pictureUrl))
            {
                post.Character.PictureUrl = pictureUrl ?? string.Empty;
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
            IsRemoved = false
        };
        _dbContext.Posts.Add(dbPost);

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

        await _dbContext.SaveChangesAsync();
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
