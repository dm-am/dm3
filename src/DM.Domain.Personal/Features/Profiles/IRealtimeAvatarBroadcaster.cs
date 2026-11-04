using System;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Direct WebSocket push about a user's avatar change, past the outbox: best-effort,
/// because a lost push only waits for the next page load.
/// </summary>
public interface IRealtimeAvatarBroadcaster
{
    /// <summary>
    /// Broadcast the avatar change. If there are no matching
    /// connections — no-op.
    /// </summary>
    /// <param name="userId">Identifier of the user whose avatar changed.</param>
    Task BroadcastAvatarChangedAsync(Guid userId);
}
