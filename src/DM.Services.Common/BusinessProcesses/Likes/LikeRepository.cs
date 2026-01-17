using System;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Common;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Common.BusinessProcesses.Likes;

/// <inheritdoc />
internal class LikeRepository : ILikeRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public LikeRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task Add(Like like)
    {
        _dbContext.Likes.Add(like);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid topicId, Guid userId)
    {
        var like = await _dbContext.Likes.FirstAsync(l => l.UserId == userId && l.EntityId == topicId);
        _dbContext.Likes.Remove(like);
        await _dbContext.SaveChangesAsync();
    }
}