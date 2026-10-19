using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public interface IGlobalChatEventRepository
{
    Task<GlobalChatEvent?> Get(Guid eventId);

    Task<IEnumerable<GlobalChatEvent>> GetByStatus(params GlobalChatEventStatus[] statuses);

    /// <summary>
    /// The live event, or null
    /// </summary>
    Task<GlobalChatEvent?> GetActiveEvent();

    Task<bool> HasActiveEvent();

    Task<bool> IsParticipant(Guid eventId, Guid userId);

    /// <summary>
    /// Create an event with its creator as the first participant
    /// </summary>
    Task<GlobalChatEvent> Create(
        CreateGlobalChatEventEntity chatEvent,
        CreateGlobalChatEventParticipantEntity creatorParticipant,
        CancellationToken ct = default);

    Task Update(UpdateGlobalChatEventEntity update, CancellationToken ct = default);

    /// <summary>
    /// Delete the event with its participants
    /// </summary>
    Task Delete(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Move the event from <paramref name="expectedStatus"/> to <paramref name="status"/>,
    /// writing only the timestamps the move carries: a move somebody else made a moment
    /// earlier is not made twice
    /// </summary>
    /// <returns>Updated event, or null when the event was no longer in the expected status</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.DuplicateEntityException">
    /// Another event holds the live status this move asks for
    /// </exception>
    Task<GlobalChatEvent?> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus expectedStatus,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Add the participant, one row per event and person settled by the write itself
    /// </summary>
    /// <returns>Created participant, or null when the person was already on the list</returns>
    Task<GlobalChatEventParticipant?> AddParticipant(
        CreateGlobalChatEventParticipantEntity participant,
        CancellationToken ct = default);

    Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default);
}
