using System;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Direct WebSocket push about a user's avatar change (transient UI hint).
/// Does not go through outbox/RabbitMQ — the event is best-effort, a lost push
/// is inconsequential (the FE picks up the change on the next page load).
///
/// Like presence updates and typing indicators: durability is not needed,
/// what matters is low delivery latency to open tabs.
///
/// The payload contains only userId — the client reloads its own data,
/// other users' avatars in the DOM update on the next render with new URLs
/// (immutable hash-based keys → browser cache safe).
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
