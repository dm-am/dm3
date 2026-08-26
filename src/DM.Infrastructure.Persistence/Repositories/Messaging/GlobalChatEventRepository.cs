using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using Microsoft.EntityFrameworkCore;
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
    public async Task<GlobalChatEvent> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default)
    {
        var chatEvent = await _dbContext.GlobalChatEvents
            .FirstAsync(e => e.GlobalChatEventId == eventId, ct).ConfigureAwait(false);

        chatEvent.Status = status;
        if (startedAt.HasValue)
        {
            chatEvent.StartedUtc = startedAt.Value;
        }
        if (endedAt.HasValue)
        {
            chatEvent.EndedUtc = endedAt.Value;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEvents
            .Where(e => e.GlobalChatEventId == eventId)
            .ProjectToGlobalChatEvent()
            .FirstAsync(ct).ConfigureAwait(false);
    }

    // ═══ PARTICIPANTS ═══

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> AddParticipant(
        CreateGlobalChatEventParticipantEntity participant,
        CancellationToken ct = default)
    {
        var dbParticipant = new DbGlobalChatEventParticipant
        {
            GlobalChatEventParticipantId = participant.GlobalChatEventParticipantId,
            GlobalChatEventId = participant.GlobalChatEventId,
            UserId = participant.UserId,
            IsOrganizer = participant.IsOrganizer,
            JoinedUtc = participant.JoinedUtc
        };

        _dbContext.GlobalChatEventParticipants.Add(dbParticipant);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventParticipantId == participant.GlobalChatEventParticipantId)
            .ProjectToGlobalChatEventParticipant()
            .FirstAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _dbContext.GlobalChatEventParticipants
            .FirstOrDefaultAsync(p => p.GlobalChatEventId == eventId && p.UserId == userId, ct).ConfigureAwait(false);

        if (participant != null)
        {
            _dbContext.GlobalChatEventParticipants.Remove(participant);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
