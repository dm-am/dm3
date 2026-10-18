using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbGlobalChatEvent = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEventParticipant;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <inheritdoc />
internal class GlobalChatEventRepository : IGlobalChatEventRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GlobalChatEventRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<GlobalChatEvent?> Get(Guid eventId) => _dbContext.GlobalChatEvents
        .Where(e => e.GlobalChatEventId == eventId)
        .ProjectToGlobalChatEvent()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GlobalChatEvent>> GetByStatus(params GlobalChatEventStatus[] statuses) =>
        await _dbContext.GlobalChatEvents
            .Where(e => statuses.Contains(e.Status))
            .OrderByDescending(e => e.StartsUtc)
            .ProjectToGlobalChatEvent()
            .ToArrayAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public Task<GlobalChatEvent?> GetActiveEvent() => _dbContext.GlobalChatEvents
        .Where(e => e.Status == GlobalChatEventStatus.Live)
        // At most one row is live on PostgreSQL, where the partial unique index
        // says so, and this order decides nothing there. It is what makes the
        // answer the same one every time where that index is not created: an
        // unordered read of several rows returns whichever one the plan reached
        // first, and the chat would name a different event on every render.
        .OrderByDescending(e => e.StartedUtc)
        .ThenBy(e => e.GlobalChatEventId)
        .ProjectToGlobalChatEvent()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<bool> HasActiveEvent() => _dbContext.GlobalChatEvents
        .AnyAsync(e => e.Status == GlobalChatEventStatus.Live);

    /// <inheritdoc />
    public Task<bool> IsParticipant(Guid eventId, Guid userId) =>
        _dbContext.GlobalChatEventParticipants
            .AnyAsync(p => p.GlobalChatEventId == eventId && p.UserId == userId);

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent> Create(
        CreateGlobalChatEventEntity chatEvent,
        CreateGlobalChatEventParticipantEntity creatorParticipant,
        CancellationToken ct = default)
    {
        var dbChatEvent = new DbGlobalChatEvent
        {
            GlobalChatEventId = chatEvent.GlobalChatEventId,
            Title = chatEvent.Title,
            Description = chatEvent.Description,
            StartsUtc = chatEvent.StartsUtc,
            Duration = chatEvent.Duration,
            IsOpen = chatEvent.IsOpen,
            Status = chatEvent.Status,
            CreatedByUserId = chatEvent.CreatedByUserId,
            CreatedUtc = chatEvent.CreatedUtc
        };

        var dbParticipant = new DbGlobalChatEventParticipant
        {
            GlobalChatEventParticipantId = creatorParticipant.GlobalChatEventParticipantId,
            GlobalChatEventId = creatorParticipant.GlobalChatEventId,
            UserId = creatorParticipant.UserId,
            IsOrganizer = creatorParticipant.IsOrganizer,
            JoinedUtc = creatorParticipant.JoinedUtc
        };

        _dbContext.GlobalChatEvents.Add(dbChatEvent);
        _dbContext.GlobalChatEventParticipants.Add(dbParticipant);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEvents
            .Where(e => e.GlobalChatEventId == chatEvent.GlobalChatEventId)
            .ProjectToGlobalChatEvent()
            .FirstAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Update(UpdateGlobalChatEventEntity update, CancellationToken ct = default)
    {
        var dbEvent = await _dbContext.GlobalChatEvents.FindAsync(new object[] { update.GlobalChatEventId }, ct);
        if (dbEvent == null)
        {
            throw new InvalidOperationException($"GlobalChatEvent {update.GlobalChatEventId} not found");
        }

        if (update.Title != null)
            dbEvent.Title = update.Title;
        if (update.Description != null)
            dbEvent.Description = update.Description;
        if (update.StartsUtc.HasValue)
            dbEvent.StartsUtc = update.StartsUtc.Value;
        if (update.Duration.HasValue)
            dbEvent.Duration = update.Duration.Value;
        if (update.IsOpen.HasValue)
            dbEvent.IsOpen = update.IsOpen.Value;
        if (update.Status.HasValue)
            dbEvent.Status = update.Status.Value;
        if (update.StartedUtc.HasValue)
            dbEvent.StartedUtc = update.StartedUtc.Value;
        if (update.EndedUtc.HasValue)
            dbEvent.EndedUtc = update.EndedUtc.Value;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Delete(Guid eventId, CancellationToken ct = default)
    {
        // Delete participants first
        var participants = await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventId == eventId)
            .ToListAsync(ct).ConfigureAwait(false);
        _dbContext.GlobalChatEventParticipants.RemoveRange(participants);

        // Then delete the event
        var chatEvent = await _dbContext.GlobalChatEvents
            .FirstOrDefaultAsync(e => e.GlobalChatEventId == eventId, ct).ConfigureAwait(false);
        if (chatEvent != null)
        {
            _dbContext.GlobalChatEvents.Remove(chatEvent);
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ═══ LIFECYCLE ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent?> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus expectedStatus,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default)
    {
        int moved;
        try
        {
            // The status the move starts from is part of the write, not of a read
            // before it. Two clicks on "start" used to read one scheduled event
            // and both write the live status over it, which stamped the moment of
            // the second one over the moment the event actually began and made the
            // announcement twice. A statement of this shape matches once.
            //
            // The two moments are written with a fallback to what is stored, so
            // the move that carries neither leaves both alone: an end must not
            // erase how long the event ran.
            moved = await _dbContext.GlobalChatEvents
                .Where(e => e.GlobalChatEventId == eventId && e.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, status)
                    .SetProperty(e => e.StartedUtc, e => startedAt ?? e.StartedUtc)
                    .SetProperty(e => e.EndedUtc, e => endedAt ?? e.EndedUtc), ct)
                .ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateKey)
        {
            // The partial unique index over the live status: another event went
            // live between the caller's check and this write. Translated here so
            // the domain does not have to know the storage engine's error codes.
            throw new DuplicateEntityException("Concurrent live global chat event", ex);
        }

        if (moved == 0)
        {
            return null;
        }

        return await _dbContext.GlobalChatEvents
            .Where(e => e.GlobalChatEventId == eventId)
            .ProjectToGlobalChatEvent()
            .FirstAsync(ct).ConfigureAwait(false);
    }

    // ═══ PARTICIPANTS ═══

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant?> AddParticipant(
        CreateGlobalChatEventParticipantEntity participant,
        CancellationToken ct = default)
    {
        // The unique pair is what says a person is on one list once, and the
        // server settles it: a read before the insert answers for the moment it
        // ran, and two clicks arriving together both pass it. Raw, because
        // ON CONFLICT is not something the change tracker can express.
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GlobalChatEventParticipants"
                ("GlobalChatEventParticipantId", "GlobalChatEventId", "UserId", "IsOrganizer", "JoinedUtc")
            VALUES ({participant.GlobalChatEventParticipantId}, {participant.GlobalChatEventId},
                    {participant.UserId}, {participant.IsOrganizer}, {participant.JoinedUtc})
            ON CONFLICT ("GlobalChatEventId", "UserId") DO NOTHING
            """, ct).ConfigureAwait(false);

        if (inserted == 0)
        {
            return null;
        }

        return await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventParticipantId == participant.GlobalChatEventParticipantId)
            .ProjectToGlobalChatEventParticipant()
            .FirstAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default) =>
        // Everything the pair holds, not the one row a read happened to return:
        // leaving has to leave, whatever the list looked like when it was written.
        _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventId == eventId && p.UserId == userId)
            .ExecuteDeleteAsync(ct);

    /// <summary>The unique violation PostgreSQL answers a broken index with.</summary>
    private const string DuplicateKey = "23505";
}
