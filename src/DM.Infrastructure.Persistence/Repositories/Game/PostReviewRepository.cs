using Npgsql;
using DM.Domain.Core.Exceptions;
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
using DM.Domain.Game.Features.PostReviews;
using Microsoft.EntityFrameworkCore;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IPostReviewRepository" />
internal class PostReviewRepository : IPostReviewRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PostReviewRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountAsync(Guid postId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.Count")
        .Where(r => r.PostId == postId && !r.IsRemoved)
        .CountAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<PostReview>> GetAsync(Guid postId, PagingData paging)
    {
        var reviews = await _dbContext.PostReviews
            .TagWith("DM.PostReview.GetByPost")
            .Where(r => r.PostId == postId && !r.IsRemoved)
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await FillLikesAsync(reviews);
        return reviews;
    }

    /// <inheritdoc />
    public async Task<PostReview?> GetAsync(Guid id)
    {
        var review = await _dbContext.PostReviews
            .TagWith("DM.PostReview.GetById")
            .Where(r => !r.IsRemoved && r.PostReviewId == id)
            .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (review != null)
        {
            await FillLikesAsync(new[] { review });
        }

        return review;
    }

    /// <inheritdoc />
    public Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.GetByAuthor")
        .Where(r => r.PostId == postId &&
                    r.AuthorId == authorId &&
                    !r.IsRemoved)
        .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountAllAsync(PostReviewFilter? filter = null)
    {
        var query = _dbContext.PostReviews
            .TagWith("DM.PostReview.CountAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PostReview>> GetAllAsync(PagingData paging, PostReviewFilter? filter = null)
    {
        var query = _dbContext.PostReviews
            .TagWith("DM.PostReview.GetAll")
            .Where(r => !r.IsRemoved);

        query = ApplyFilter(query, filter);

        var reviews = await query
            .OrderByDescending(r => r.CreatedUtc)
            .Page(paging)
            .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

        await FillLikesAsync(reviews);
        return reviews;
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid postId) => _dbContext.PostReviews
        .TagWith("DM.PostReview.Exists")
        .AnyAsync(r => r.AuthorId == authorId &&
                       r.PostId == postId &&
                       !r.IsRemoved);

    /// <inheritdoc />
    public async Task<PostReview> CreateAsync(CreatePostReviewEntity entity)
    {
        var dbReview = new DbPostReview
        {
            PostReviewId = entity.PostReviewId,
            AuthorId = entity.AuthorId,
            PostId = entity.PostId,
            PostAuthorId = entity.PostAuthorId,
            GameId = entity.GameId,
            CreatedUtc = entity.CreatedUtc,
            Sign = entity.Sign,
            Text = entity.Text,
            IsRemoved = false
        };

        _dbContext.PostReviews.Add(dbReview);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The caller pre-checks for an existing post review; this is the race
            // where two concurrent requests both pass that check. Translated here
            // so the domain does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate post review", ex);
        }

        return await _dbContext.PostReviews
            .TagWith("DM.PostReview.Created")
            .Where(r => r.PostReviewId == dbReview.PostReviewId)
            .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<PostReview> UpdateAsync(UpdatePostReviewEntity entity)
    {
        var dbReview = await _dbContext.PostReviews.FindAsync(entity.PostReviewId);
        if (dbReview == null)
        {
            throw new InvalidOperationException($"Review {entity.PostReviewId} not found");
        }

        if (entity.Sign.HasValue)
            dbReview.Sign = entity.Sign.Value;
        if (entity.IsRemoved.HasValue)
            dbReview.IsRemoved = entity.IsRemoved.Value;
        if (entity.ModifiedUtc.HasValue)
            dbReview.ModifiedUtc = entity.ModifiedUtc.Value;
        if (entity.ModifiedByUserId.HasValue)
            dbReview.ModifiedByUserId = entity.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.PostReviews
            .TagWith("DM.PostReview.Updated")
            .Where(r => r.PostReviewId == entity.PostReviewId)
            .ProjectTo<PostReview>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task UpdateUserQualityRatingAsync(Guid userId, int ratingDelta)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.QualityRating += ratingDelta;
            await _dbContext.SaveChangesAsync();
        }
    }

    // ═══ ELIGIBILITY ═══

    /// <inheritdoc />
    public async Task<PostInfo?> GetPostInfoAsync(Guid postId)
    {
        return await _dbContext.Posts
            .TagWith("DM.PostReview.GetPostInfo")
            .Where(p => p.PostId == postId)
            .Select(p => new PostInfo
            {
                AuthorId = p.AuthorId,
                GameId = p.Room.GameId
            })
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId, DateTimeOffset cutoffDate) =>
        _dbContext.PostReviews
            .TagWith("DM.PostReview.HasRecentInGame")
            .AnyAsync(r => r.AuthorId == authorId &&
                           r.GameId == gameId &&
                           r.CreatedUtc >= cutoffDate &&
                           !r.IsRemoved);

    /// <inheritdoc />
    /// <remarks>
    /// The denormalised counter, the same one the newbie badge is computed from;
    /// a COUNT over posts is a second number and drifts from it on every removal.
    /// </remarks>
    public Task<int> GetUserPostCountAsync(Guid userId) => _dbContext.Users
        .TagWith("DM.PostReview.UserPostCount")
        .Where(u => u.UserId == userId)
        .Select(u => u.QuantityRating)
        .FirstOrDefaultAsync();

    // ═══ PRIVATE ═══

    /// <summary>
    /// Fills <see cref="PostReview.Likes"/> for a page of reviews.
    /// </summary>
    /// <remarks>
    /// Likes are polymorphic (EntityType + EntityId), so no navigation points at
    /// them and the mapping profile leaves the collection empty. Two batched
    /// lookups per page fill it — the like rows, then their authors — instead of
    /// a correlated subquery per row (PERFORMANCE.md → "Avoid inline
    /// aggregations"), which is the shape TopicRepository already uses for its
    /// like counts.
    /// </remarks>
    private async Task FillLikesAsync(IReadOnlyCollection<PostReview> reviews)
    {
        if (reviews.Count == 0)
        {
            return;
        }

        // List<Guid>, not Guid[]: EF Core's translator has a Guid[] edge case on
        // the ReadOnlySpan<Guid> interpreter path (see TopicRepository).
        var reviewIds = reviews.Select(r => r.Id).ToList();
        var likes = await _dbContext.Likes
            .TagWith("DM.PostReview.Likes")
            .AsNoTracking()
            .Where(l => !l.IsRemoved &&
                        l.EntityType == LikeEntityType.PostReview &&
                        reviewIds.Contains(l.EntityId))
            .Select(l => new { l.EntityId, l.UserId })
            .ToListAsync();

        if (likes.Count == 0)
        {
            return;
        }

        var likerIds = likes.Select(l => l.UserId).Distinct().ToList();
        var likers = await _dbContext.Users
            .TagWith("DM.PostReview.Likers")
            .AsNoTracking()
            .Where(u => likerIds.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToDictionaryAsync(u => u.UserId);

        var byReview = likes
            .GroupBy(l => l.EntityId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(l => likers.ContainsKey(l.UserId))
                    .Select(l => likers[l.UserId])
                    .ToArray());

        foreach (var review in reviews)
        {
            if (byReview.TryGetValue(review.Id, out var users))
            {
                review.Likes = users;
            }
        }
    }

    private IQueryable<DbPostReview> ApplyFilter(IQueryable<DbPostReview> query, PostReviewFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.AuthorId.HasValue)
            query = query.Where(r => r.AuthorId == filter.AuthorId.Value);

        if (filter.RecipientId.HasValue)
            query = query.Where(r => r.PostAuthorId == filter.RecipientId.Value);

        if (filter.GameId.HasValue)
            query = query.Where(r => r.GameId == filter.GameId.Value);

        return query;
    }
}
