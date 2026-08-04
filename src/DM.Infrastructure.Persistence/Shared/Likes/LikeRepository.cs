using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    public async Task Add(Entities.Shared.Like like)
    {
        _dbContext.Likes.Add(like);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The caller pre-checks the loaded navigation; this is the race where two
            // clicks both pass that check. Translated here so the domain does not have
            // to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate like", ex);
        }
    }

    /// <inheritdoc />
    public async Task Delete(Guid entityId, Guid userId)
    {
        await _dbContext.Likes
            .Where(l => l.UserId == userId && l.EntityId == entityId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRemoved, true));
    }
}
