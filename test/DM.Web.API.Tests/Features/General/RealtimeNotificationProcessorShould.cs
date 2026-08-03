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
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// A push reaches the connections of its recipients, and nobody else.
/// </summary>
/// <remarks>
/// Two addressing modes share one method and which one a message gets is decided
/// by a single equality against the global chat event. Global chat is public, it
/// carries no recipient list and goes to every open connection, guests included.
/// Everything else is personal and may only ever reach the connections the map
/// holds, because the map is the whole of the guarantee that an anonymous
/// connection cannot become a per-user target - a guarantee stated in the
/// comments of the hub and of the connection service, and until now held by
/// nothing else. An early return for guests, or a fallback to broadcasting when
/// nobody is connected, would break it without failing anything.
/// </remarks>
public class RealtimeNotificationProcessorShould : UnitTestBase
{
    private static readonly Guid Recipient = Guid.NewGuid();

    private readonly Mock<IUserConnectionService> _connections;
    private readonly Mock<IHubClients<INotificationHub>> _clients;
    private readonly Mock<INotificationHub> _broadcast;
    private readonly Mock<INotificationHub> _targeted;
    private readonly RealtimeNotificationProcessor _processor;

    public RealtimeNotificationProcessorShould()
    {
        _connections = Mock<IUserConnectionService>();
        _clients = Mock<IHubClients<INotificationHub>>();
        _broadcast = Mock<INotificationHub>();
        _targeted = Mock<INotificationHub>();

        _clients.SetupGet(clients => clients.All).Returns(_broadcast.Object);
        _clients
            .Setup(clients => clients.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Returns(_targeted.Object);

        var hubContext = Mock<IHubContext<NotificationHub, INotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(_clients.Object);

        _processor = new RealtimeNotificationProcessor(
            NullLogger<RealtimeNotificationProcessor>.Instance,
            _connections.Object,
            hubContext.Object);
    }

    [Fact]
    public async Task BroadcastAGlobalChatMessageToEveryConnection()
    {
        await Process(EventType.NewGlobalChatMessage);

        _broadcast.Verify(hub => hub.Send(It.IsAny<Notification>()), Times.Once);
        _connections.Verify(connections => connections.GetConnectedUsers(), Times.Never);
    }

    [Fact]
    public async Task SendAPersonalEventOnlyToTheConnectionsOfItsRecipients()
    {
        _connections
            .Setup(connections => connections.GetConnectedUsers())
            .Returns(new Dictionary<Guid, IEnumerable<string>>
            {
                [Recipient] = new[] { "recipient-1", "recipient-2" },
                [Guid.NewGuid()] = new[] { "stranger-1" },
            });

        await Process(EventType.NewMessage);

        _broadcast.Verify(hub => hub.Send(It.IsAny<Notification>()), Times.Never);
        _clients.Verify(
            clients => clients.Clients(It.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 2 &&
                ids.Contains("recipient-1") &&
                ids.Contains("recipient-2"))),
            Times.Once);
    }

    /// <summary>
    /// Nobody connected is an empty address list, not an audience of everyone.
    /// </summary>
    [Fact]
    public async Task SendAPersonalEventNowhereWhenNoRecipientIsConnected()
    {
        _connections
            .Setup(connections => connections.GetConnectedUsers())
            .Returns(new Dictionary<Guid, IEnumerable<string>>());

        await Process(EventType.NewMessage);

        _broadcast.Verify(hub => hub.Send(It.IsAny<Notification>()), Times.Never);
        _clients.Verify(
            clients => clients.Clients(It.Is<IReadOnlyList<string>>(ids => ids.Count == 0)),
            Times.Once);
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
