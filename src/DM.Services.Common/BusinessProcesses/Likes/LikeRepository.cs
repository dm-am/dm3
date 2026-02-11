using System;
using System.Linq;
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
        await _dbContext.Likes
            .Where(l => l.UserId == userId && l.EntityId == topicId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRemoved, true));
    }
}