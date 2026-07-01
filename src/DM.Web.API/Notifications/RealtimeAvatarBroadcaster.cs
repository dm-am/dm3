using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Notifications;

/// <inheritdoc />
internal class RealtimeAvatarBroadcaster : IRealtimeAvatarBroadcaster
{
    private readonly IUserConnectionService _connections;
    private readonly IHubContext<NotificationHub, INotificationHub> _hub;
    private readonly ILogger<RealtimeAvatarBroadcaster> _logger;

    /// <inheritdoc />
    public RealtimeAvatarBroadcaster(
        IUserConnectionService connections,
        IHubContext<NotificationHub, INotificationHub> hub,
        ILogger<RealtimeAvatarBroadcaster> logger)
    {
        _connections = connections;
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BroadcastAvatarChangedAsync(Guid userId)
    {
        // Best-effort: если ошибка в SignalR — не падаем; основная операция
        // (DB-update + S3 PUT) уже закоммичена, push — bonus.
        try
        {
            var connections = _connections.GetConnectedUsers();
            // Broadcast всем подключенным: чужие вкладки тоже видят новый
            // аватар в чатах и комментариях после re-render.
            var connectionIds = connections.Values.SelectMany(ids => ids).ToArray();
            if (connectionIds.Length == 0) return;

            await _hub.Clients.Clients(connectionIds).Send(new Notification
            {
                Id = Guid.NewGuid(),
                EventType = EventType.UserAvatarChanged,
                Payload = new { userId },
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast UserAvatarChanged for {UserId}", userId);
        }
    }
}
