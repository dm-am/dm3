using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Participants;

/// <summary>
/// Service for managing chat event participants
/// </summary>
public interface IGlobalChatEventParticipantService
{
    /// <summary>
    /// Get participants of an event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>List of participants</returns>
    Task<IEnumerable<GlobalChatEventParticipant>> GetParticipants(Guid eventId);

    /// <summary>
    /// Join an open event (self-service for users)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> Join(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Leave an event (self-service for participants)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Leave(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Add participant to event (organizer action)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> AddParticipant(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove participant from event (organizer action)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default);
}
