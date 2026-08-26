using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Blog;
using Microsoft.EntityFrameworkCore;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogBlacklistRepository" />
internal class BlogBlacklistRepository : IBlogBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    public BlogBlacklistRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetBlacklist(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogBlacklists
            .Where(b => b.BlogId == blogId)
            .Select(b => b.BlockedUser)
            .ProjectToGeneralUser()
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.BlogBlacklists
            .AnyAsync(b => b.BlogId == blogId && b.BlockedUserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task Add(Guid blogId, Guid blockedUserId, Guid blockedByUserId, CancellationToken ct = default)
    {
        var entry = new BlogBlacklist
        {
            EntryId = _guidFactory.Create(),
            BlogId = blogId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = _dateTimeProvider.Now
        };

        _dbContext.BlogBlacklists.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task Remove(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        var entry = await _dbContext.BlogBlacklists
            .FirstOrDefaultAsync(b => b.BlogId == blogId && b.BlockedUserId == userId, ct);

        if (entry != null)
        {
            _dbContext.BlogBlacklists.Remove(entry);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> CancelInvitationsForUser(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        var pendingInvitations = await _dbContext.Tokens
            .Where(t => !t.IsRemoved &&
                        t.EntityId == blogId &&
                        t.UserId == userId &&
                        (t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation))
            .Select(t => t.TokenId)
            .ToListAsync(ct);

        if (pendingInvitations.Count > 0)
        {
            await _dbContext.Tokens
                .Where(t => pendingInvitations.Contains(t.TokenId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRemoved, true), ct);
        }

        return pendingInvitations;
    }

    /// <inheritdoc />
    public async Task<int> CopyFromPersonalBlacklist(Guid blogId, Guid ownerId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;

        // Get personal blacklist entries that are not already in blog blacklist
        var personalBlacklistUserIds = await _dbContext.UserBlacklists
            .Where(ub => ub.OwnerId == ownerId)
            .Select(ub => ub.BlockedUserId)
            .ToListAsync(ct);

        var existingBlogBlacklistUserIds = await _dbContext.BlogBlacklists
            .Where(bb => bb.BlogId == blogId)
            .Select(bb => bb.BlockedUserId)
            .ToListAsync(ct);

        var toAdd = personalBlacklistUserIds.Except(existingBlogBlacklistUserIds).ToList();

        foreach (var blockedUserId in toAdd)
        {
            var entry = new BlogBlacklist
            {
                EntryId = _guidFactory.Create(),
                BlogId = blogId,
                BlockedUserId = blockedUserId,
                BlockedByUserId = ownerId,
                CreatedUtc = now
            };
            _dbContext.BlogBlacklists.Add(entry);
        }

        if (toAdd.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return toAdd.Count;
    }
}
