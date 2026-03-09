using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Mentorships;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class MentorshipRepository : IMentorshipRepository
{
    private readonly DmDbContext _dbContext;

    public MentorshipRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetGameMentorId(Guid gameId, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(g => g.GameId == gameId && !g.IsRemoved)
            .Select(g => g.MentorId)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetGameMentor(Guid gameId, Guid? mentorId, CancellationToken ct = default)
    {
        await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.MentorId, mentorId), ct);
    }

    /// <inheritdoc />
    public async Task<bool> GameExists(Guid gameId, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .AnyAsync(g => g.GameId == gameId && !g.IsRemoved, ct);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetBlogMentorId(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Where(b => b.BlogId == blogId && !b.IsRemoved)
            .Select(b => b.MentorId)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetBlogMentor(Guid blogId, Guid? mentorId, CancellationToken ct = default)
    {
        await _dbContext.Blogs
            .Where(b => b.BlogId == blogId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.MentorId, mentorId), ct);
    }

    /// <inheritdoc />
    public async Task<bool> BlogExists(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .AnyAsync(b => b.BlogId == blogId && !b.IsRemoved, ct);
    }
}
