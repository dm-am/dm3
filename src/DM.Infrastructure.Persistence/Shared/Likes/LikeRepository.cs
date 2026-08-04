using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <inheritdoc />
internal class LikeRepository : ILikeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public LikeRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
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
    public Task<bool> Exists(Guid entityId, Guid userId) => _dbContext.Likes
        .AnyAsync(l => !l.IsRemoved && l.EntityId == entityId && l.UserId == userId);

    /// <inheritdoc />
    public async Task Delete(Guid entityId, Guid userId)
    {
        // Set outside the expression tree: ExecuteUpdate builds one UPDATE and the clock has
        // to be read before it, not per row. The author of the removal is the reader who
        // unliked — a like is withdrawn by the person who left it and by nobody else.
        var deletedUtc = _dateTimeProvider.Now;
        await _dbContext.Likes
            .Where(l => l.UserId == userId && l.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.IsRemoved, true)
                .SetProperty(l => l.DeletedByUserId, (Guid?)userId)
                .SetProperty(l => l.DeletedUtc, (DateTimeOffset?)deletedUtc));
    }
}
