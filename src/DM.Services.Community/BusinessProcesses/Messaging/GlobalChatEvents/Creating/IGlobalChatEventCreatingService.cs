using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <summary>
/// Service for creating chat events
/// </summary>
public interface IGlobalChatEventCreatingService
{
    /// <summary>
    /// Create a new chat event
    /// </summary>
    /// <param name="createGlobalChatEvent">Creation DTO</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created event</returns>
    Task<GlobalChatEvent> Create(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default);
}
