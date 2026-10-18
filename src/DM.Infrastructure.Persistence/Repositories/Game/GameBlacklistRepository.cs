using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Blacklists;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using Microsoft.EntityFrameworkCore;
using Npgsql;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameBlacklistRepository : IGameBlacklistRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public GameBlacklistRepository(
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
    /// Oldest entry first, so a name just added is read last, at the bottom of
    /// the list. The copy of the personal list writes one moment on every row,
    /// and the name settles those. The order is set before the select: the
    /// moment belongs to the entry, not to the user it reads.
    /// </remarks>
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId) => await _dbContext.GameBlacklists
        .Where(b => b.GameId == gameId)
        .OrderBy(b => b.CreatedUtc)
        .ThenBy(b => b.BlockedUser.Username)
        .Select(b => b.BlockedUser)
        .ProjectToGeneralUser()
        .ToArrayAsync();

    /// <inheritdoc />
    /// <remarks>
    /// One statement, and the skip is the database's. The copy used to read the
    /// game's list and insert what it did not see there, and a read cannot see a
    /// request running beside it: an entry the master added between the two
    /// statements was inserted a second time, and the unique index on
    /// (GameId, BlockedUserId) refused the whole copy with a 23505. ON CONFLICT
    /// settles each pair against the committed state instead, and the count the
    /// statement reports is the count of rows it actually wrote.
    ///
    /// The identifiers are made by the database, one per row the statement
    /// selects. Made here, they would need the owner's list read out first, and
    /// that read is the extra statement this replaces.
    /// </remarks>
    public Task<int> CopyFromPersonalBlacklist(Guid gameId, Guid ownerId, CancellationToken ct = default) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GameBlacklists" ("GameBlacklistId", "GameId", "BlockedUserId", "BlockedByUserId", "CreatedUtc")
            SELECT gen_random_uuid(), {gameId}, personal."BlockedUserId", {ownerId}, {_dateTimeProvider.Now}
            FROM "UserBlacklists" AS personal
            WHERE personal."OwnerId" = {ownerId}
            ON CONFLICT ("GameId", "BlockedUserId") DO NOTHING
            """, ct);

    public async Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId)
    {
        var blacklistEntry = new GameBlacklist
        {
            EntryId = _guidFactory.Create(),
            GameId = gameId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = _dateTimeProvider.Now
        };

        _dbContext.GameBlacklists.Add(blacklistEntry);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The caller pre-checks the game's blacklist; this is the race where two
            // concurrent requests both pass that check. Translated here so the domain
            // does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate game blacklist entry", ex);
        }

        return await _dbContext.GameBlacklists
            .Where(b => b.EntryId == blacklistEntry.EntryId)
            .Select(b => b.BlockedUser)
            .ProjectToGeneralUser()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Remove(Guid gameId, Guid userId)
    {
        // Set-based: the pair is what identifies the entry, so every row of it goes
        // in one statement. Loading one row and deleting it left a duplicate behind
        // with the person still blocked. Zero rows is the answer for a pair that is
        // not there, not a failure - the caller has already refused that case, and
        // the row can also be gone by the time the delete runs.
        await _dbContext.GameBlacklists
            .Where(b => b.GameId == gameId && b.BlockedUserId == userId)
            .ExecuteDeleteAsync();
    }
}
