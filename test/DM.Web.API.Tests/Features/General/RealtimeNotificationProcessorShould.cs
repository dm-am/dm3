using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Notifications;
using DM.Web.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

// Global chat events carry no recipients and go to every connection, guests included; everything
// else reaches only the connections the map holds, which keeps an anonymous connection from
// becoming a target.
public class RealtimeNotificationProcessorShould : UnitTestBase
{
    private static readonly Guid Recipient = Guid.NewGuid();

    private readonly IUserConnectionService _connections;
    private readonly IHubClients<INotificationHub> _clients;
    private readonly INotificationHub _broadcast;
    private readonly INotificationHub _targeted;
    private readonly RealtimeNotificationProcessor _processor;

    public RealtimeNotificationProcessorShould()
    {
        _connections = Mock<IUserConnectionService>();
        _clients = Mock<IHubClients<INotificationHub>>();
        _broadcast = Mock<INotificationHub>();
        _targeted = Mock<INotificationHub>();

        _clients.All.Returns(_broadcast);
        _clients
            .Clients(Arg.Any<IReadOnlyList<string>>()).Returns(_targeted);

        var hubContext = Mock<IHubContext<NotificationHub, INotificationHub>>();
        hubContext.Clients.Returns(_clients);

        _processor = new RealtimeNotificationProcessor(
            NullLogger<RealtimeNotificationProcessor>.Instance,
            _connections,
            hubContext);
    }

    [Fact]
    public async Task BroadcastAGlobalChatMessageToEveryConnection()
    {
        await Process(EventType.NewGlobalChatMessage);

        await _broadcast.Received(1).Send(Arg.Any<Notification>());
        _connections.DidNotReceive().GetConnectedUsers();
    }

    [Theory]
    [InlineData(EventType.GlobalChatEventStarted)]
    [InlineData(EventType.GlobalChatEventEnded)]
    public async Task BroadcastTheLifecycleOfAChatEventToEveryConnection(EventType eventType)
    {
        await Process(eventType);

        await _broadcast.Received(1).Send(Arg.Any<Notification>());
        _connections.DidNotReceive().GetConnectedUsers();
    }

    [Fact]
    public async Task SendAPersonalEventOnlyToTheConnectionsOfItsRecipients()
    {
        _connections
            .GetConnectedUsers().Returns(new Dictionary<Guid, IEnumerable<string>>
            {
                [Recipient] = new[] { "recipient-1", "recipient-2" },
                [Guid.NewGuid()] = new[] { "stranger-1" },
            });

        await Process(EventType.NewMessage);

        await _broadcast.DidNotReceive().Send(Arg.Any<Notification>());
        _clients.Received(1).Clients(Arg.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 2 &&
                ids.Contains("recipient-1") &&
                ids.Contains("recipient-2")));
    }

    // Nobody connected is an empty address list, not an audience of everyone.
    [Fact]
    public async Task SendAPersonalEventNowhereWhenNoRecipientIsConnected()
    {
        _connections
            .GetConnectedUsers().Returns(new Dictionary<Guid, IEnumerable<string>>());

        await Process(EventType.NewMessage);

        await _broadcast.DidNotReceive().Send(Arg.Any<Notification>());
        _clients.Received(1).Clients(Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 0));
    }

    private async Task Process(EventType eventType) =>
        await _processor.Process(
            "routing-key",
            new RealtimeNotification
            {
                NotificationId = Guid.NewGuid(),
                EventType = eventType,
                RecipientIds = new[] { Recipient },
                Metadata = new { },
            },
            CancellationToken.None);
}
