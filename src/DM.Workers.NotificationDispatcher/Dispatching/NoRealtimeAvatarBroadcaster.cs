using System;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The answer this host gives to "push the new avatar to the connected
/// clients": there are none here.
/// </summary>
/// <remarks>
/// The SignalR hub and its connection map live in the API, and an avatar
/// changes over a request - never over a queue message. The dependency exists
/// because this host scans the personal domain in, whose user service declares
/// the broadcast for the API's sake, and the container refuses to start over an
/// unconstructible registration since the composition validates on build.
///
/// Throws rather than doing nothing, for the same reason NoIdentityProvider
/// does: a delivery quietly skipped is a real answer to a question that has no
/// clients behind it, and a path that reaches this is a path that has to be
/// looked at rather than served.
/// </remarks>
internal sealed class NoRealtimeAvatarBroadcaster : IRealtimeAvatarBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAvatarChangedAsync(Guid userId) => throw new InvalidOperationException(
        "This host consumes messages and holds no client connections, so there is nothing to " +
        "broadcast an avatar to. Something on this path asked for a realtime push, which is a " +
        "defect in the path rather than a delivery to skip.");
}
