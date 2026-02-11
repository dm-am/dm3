using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <inheritdoc />
internal class ReviewCreatingRepository : IReviewCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReviewCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<bool> UserHasReview(Guid userId)
    {
        return await _dbContext.Reviews
            .AnyAsync(r => r.UserId == userId && !r.IsRemoved);
    }

    /// <inheritdoc />
    public async Task<Review> Create(DataAccess.BusinessObjects.Common.Review review)
    {
        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Reviews
            .Where(r => r.ReviewId == review.ReviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<PostInfo?> GetPostInfo(Guid postId)
    {
        return await _dbContext.Posts
            .Where(p => p.PostId == postId)
            .Include(p => p.Room)
            .Select(p => new PostInfo
            {
                AuthorId = p.UserId,
                GameId = p.Room.GameId
            })
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Review?> GetPostReviewByAuthor(Guid postId, Guid authorId)
    {
        return await _dbContext.Reviews
            .Where(r => r.TargetType == ReviewTargetType.Post &&
                        r.TargetId == postId &&
                        r.UserId == authorId &&
                        !r.IsRemoved)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}