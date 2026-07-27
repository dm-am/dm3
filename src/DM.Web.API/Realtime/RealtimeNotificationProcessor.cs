using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Notifications;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Realtime;

internal class RealtimeNotificationProcessor(
    ILogger<RealtimeNotificationProcessor> logger,
    IUserConnectionService connectionService,
    IHubContext<NotificationHub, INotificationHub> hubContext)
    : IProcessor<string, RealtimeNotification>
{
    public async Task<ProcessResult> Process(
        string key, RealtimeNotification message, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = message.NotificationId,
            EventType = message.EventType,
            Payload = message.Metadata
        };

        if (message.EventType == EventType.NewGlobalChatMessage)
        {
            // Global chat is public: its message events carry no stored
            // recipient list and go to every open connection, including
            // anonymous (guest) ones. Guests are never registered in the
            // user connection map, so this broadcast path is the only
            // event they can ever receive.
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