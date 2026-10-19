using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Global chat events: CRUD, lifecycle and participants
/// </summary>
public interface IGlobalChatEventService
{
    Task<GlobalChatEvent> CreateAsync(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default);

    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">If event not found</exception>
    Task<GlobalChatEvent> GetAsync(Guid eventId);

    /// <summary>
    /// The live event, or null
    /// </summary>
    Task<GlobalChatEvent?> GetActiveEventAsync();

    /// <summary>
    /// Every event not ended yet, live and scheduled
    /// </summary>
    Task<IEnumerable<GlobalChatEvent>> GetAllAsync();

    Task<GlobalChatEvent> UpdateAsync(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default);

    Task DeleteAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Start a scheduled event
    /// </summary>
    Task<GlobalChatEvent> StartAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// End a live event
    /// </summary>
    Task<GlobalChatEvent> EndAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Join an open event
    /// </summary>
    Task<GlobalChatEventParticipant> JoinAsync(Guid eventId, CancellationToken ct = default);

    Task LeaveAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Add a participant, as the organizer
    /// </summary>
    Task<GlobalChatEventParticipant> AddParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove a participant, as the organizer
    /// </summary>
    Task RemoveParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default);
}
