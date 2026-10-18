using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Entities.Blog;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    /// <remarks>
    /// The game's order (GameBlacklistRepository.Get): oldest entry first, and
    /// by name among the rows the copy of the personal list wrote at one moment.
    /// </remarks>
    public async Task<IEnumerable<GeneralUser>> GetBlacklist(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogBlacklists
            .Where(b => b.BlogId == blogId)
            .OrderBy(b => b.CreatedUtc)
            .ThenBy(b => b.BlockedUser.Username)
            .Select(b => b.BlockedUser)
            .ProjectToGeneralUser()
            .ToArrayAsync(ct);
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
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The race the caller's check cannot see: two requests for the same
            // name both passed it. Translated here, as the game's list does, so
            // the domain does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate blog blacklist entry", ex);
        }
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
    /// <remarks>
    /// One statement, the way the game copies the same list
    /// (GameBlacklistRepository.CopyFromPersonalBlacklist), and with the same
    /// skip: ON CONFLICT on the unique index over the pair settles each entry
    /// against the committed state, so an entry added beside the copy is not
    /// inserted twice and does not refuse the whole copy with a 23505.
    /// </remarks>
    public Task<int> CopyFromPersonalBlacklist(Guid blogId, Guid ownerId, CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "BlogBlacklists" ("EntryId", "BlogId", "BlockedUserId", "BlockedByUserId", "CreatedUtc")
            SELECT gen_random_uuid(), {blogId}, personal."BlockedUserId", {ownerId}, {_dateTimeProvider.Now}
            FROM "UserBlacklists" AS personal
            WHERE personal."OwnerId" = {ownerId}
            ON CONFLICT ("BlogId", "BlockedUserId") DO NOTHING
            """, ct);
}
