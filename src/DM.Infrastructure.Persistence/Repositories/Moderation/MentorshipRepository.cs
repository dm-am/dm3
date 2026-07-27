using System;
using System.Collections.Generic;
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

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MentorshipAssignment>> GetGameMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(g => !g.IsRemoved && g.MentorId.HasValue && mentorIds.Contains(g.MentorId.Value))
            .OrderBy(g => g.Title)
            .Select(g => new MentorshipAssignment
            {
                MentorId = g.MentorId!.Value,
                TargetId = g.GameId,
                Title = g.Title
            })
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MentorshipAssignment>> GetBlogMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Where(b => !b.IsRemoved && b.MentorId.HasValue && mentorIds.Contains(b.MentorId.Value))
            .OrderBy(b => b.Title)
            .Select(b => new MentorshipAssignment
            {
                MentorId = b.MentorId!.Value,
                TargetId = b.BlogId,
                Title = b.Title
            })
            .ToArrayAsync(ct);
    }
}
