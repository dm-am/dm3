using System.Threading;
using System.Threading.Tasks;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEventParticipant;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <summary>
/// Repository for creating chat events
/// </summary>
public interface IGlobalChatEventCreatingRepository
{
    /// <summary>
    /// Create a chat event with the creator as initial participant
    /// </summary>
    /// <param name="GlobalChatEvent">Event entity</param>
    /// <param name="creatorParticipant">Creator participant entity</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created event</returns>
    Task<Reading.GlobalChatEvent> Create(
        DbGlobalChatEvent GlobalChatEvent,
        DbGlobalChatEventParticipant creatorParticipant,
        CancellationToken ct = default);
}
