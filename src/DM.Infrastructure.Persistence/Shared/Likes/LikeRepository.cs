using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Likes;

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
    public Task Add(Entities.CrossDomain.Like like)
    {
        _dbContext.Likes.Add(like);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid entityId, Guid userId)
    {
        await _dbContext.Likes
            .Where(l => l.UserId == userId && l.EntityId == entityId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRemoved, true));
    }
}
