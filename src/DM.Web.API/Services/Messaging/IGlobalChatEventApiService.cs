using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Messaging;

namespace DM.Web.API.Services.Messaging;

/// <summary>
/// API service for chat events
/// </summary>
public interface IGlobalChatEventApiService
{
    /// <summary>
    /// Get list of events (upcoming and live)
    /// </summary>
    Task<ListEnvelope<GlobalChatEventSummary>> GetList(CancellationToken ct = default);

    /// <summary>
    /// Get event details
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Get(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get active (live) event
    /// </summary>
    Task<Envelope<GlobalChatEventSummary>?> GetActive(CancellationToken ct = default);

    /// <summary>
    /// Create new event
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Create(CreateGlobalChatEventInput input, CancellationToken ct = default);

    /// <summary>
    /// Update event
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Update(Guid id, UpdateGlobalChatEventInput input, CancellationToken ct = default);

    /// <summary>
    /// Delete event
    /// </summary>
    Task Delete(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Start event (change status to Live)
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Start(Guid id, CancellationToken ct = default);

    /// <summary>
    /// End event (change status to Ended)
    /// </summary>
    Task<Envelope<GlobalChatEvent>> End(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Join open event
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Join(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Leave event
    /// </summary>
    Task<Envelope<GlobalChatEvent>> Leave(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Add participant to closed event (organizer only)
    /// </summary>
    Task<Envelope<GlobalChatEvent>> AddParticipant(Guid id, AddParticipantInput input, CancellationToken ct = default);

    /// <summary>
    /// Remove participant from event (organizer only)
    /// </summary>
    Task RemoveParticipant(Guid eventId, string login, CancellationToken ct = default);
}
