using System;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The answer this host gives to "push the new avatar to the connected
/// clients": there are none here.
/// </summary>
/// <remarks>
/// Throws rather than skipping, for the reason NoIdentityProvider gives.
/// </remarks>
internal sealed class NoRealtimeAvatarBroadcaster : IRealtimeAvatarBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAvatarChangedAsync(Guid userId) => throw new InvalidOperationException(
        "This host consumes messages and holds no client connections, so there is nothing to " +
        "broadcast an avatar to. Something on this path asked for a realtime push, which is a " +
        "defect in the path rather than a delivery to skip.");
}
