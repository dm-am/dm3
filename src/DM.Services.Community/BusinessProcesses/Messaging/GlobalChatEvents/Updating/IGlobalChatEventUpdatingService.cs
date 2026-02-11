using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Updating;

/// <summary>
/// Service for updating chat events
/// </summary>
public interface IGlobalChatEventUpdatingService
{
    /// <summary>
    /// Update a chat event
    /// </summary>
    /// <param name="updateGlobalChatEvent">Update DTO</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> Update(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default);
}
