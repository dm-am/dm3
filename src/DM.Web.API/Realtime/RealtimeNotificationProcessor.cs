using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Realtime;

internal class RealtimeNotificationProcessor(
    ILogger<RealtimeNotificationProcessor> logger,
    IUserConnectionService connectionService,
    IHubContext<NotificationHub, INotificationHub> hubContext)
    : IProcessor<string, RealtimeNotification>
{
    /// <summary>
    /// The events of the global chat itself, which go to every open connection.
    /// </summary>
    /// <remarks>
    /// Global chat is public and addressed to nobody in particular, so these reach
    /// guests too; through the connection map they would reach nobody at all.
    /// </remarks>
    private static readonly HashSet<EventType> Broadcast =
    [
        EventType.NewGlobalChatMessage,
        EventType.GlobalChatEventStarted,
        EventType.GlobalChatEventEnded
    ];

    public async Task<ProcessResult> Process(
        string key, RealtimeNotification message, CancellationToken cancellationToken)
    {
        // The mapper of the list, so the open tab reads one shape from both transports.
        var notification = NotificationMapper.ToNotification(message, isRead: false);

        if (Broadcast.Contains(message.EventType))
        {
            await hubContext.Clients.All.Send(notification);
        }
        else
        {
            var connectedUsers = connectionService.GetConnectedUsers();
            var connectionIds = message.RecipientIds
                .Where(connectedUsers.ContainsKey)
                .SelectMany(id => connectedUsers[id])
                .ToArray();
            await hubContext.Clients.Clients(connectionIds).Send(notification);
        }

        logger.LogDebug("Message published: {Message} for users {Users}", message.Metadata, message.RecipientIds);
        return ProcessResult.Success;
    }
}
