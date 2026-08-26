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
    /// Global chat is public: these carry no stored recipient list and reach
    /// every connection, including anonymous (guest) ones. Guests are never
    /// registered in the user connection map, so this broadcast path is the only
    /// event they can ever receive. Besides a new message it carries the start
    /// and the end of a chat event: both change what the strip above the feed
    /// says, for reader and participant alike, and neither is addressed to
    /// anybody in particular. Addressing them through the map would deliver them
    /// to nobody at all.
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
        var notification = new Notification
        {
            Id = message.NotificationId,
            EventType = message.EventType,
            Payload = message.Metadata
        };

        if (Broadcast.Contains(message.EventType))
        {
            await hubContext.Clients.All.Send(notification);
        }
        else
        {
            // Per-user events are resolved strictly through the connection
            // map, which only ever contains authenticated connections —
            // an anonymous connection cannot appear here by construction
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
