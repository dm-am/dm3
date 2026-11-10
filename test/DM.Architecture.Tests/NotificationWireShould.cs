using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Notifications;
using DM.Web.API.Realtime;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace DM.Architecture.Tests;

/// <remarks>
/// The DTO leaves the API over REST and over the hub, and a dictionary key is not touched by
/// PropertyNamingPolicy; the dictionary arm stands in for a writer handing the bag under CLR names.
/// Nothing fails when they disagree.
/// </remarks>
public class NotificationWireShould
{
    /// <summary>Metadata as the dispatcher published it to the bus.</summary>
    private const string BusMetadata = """{"gameTitle":"Frozen Keep","daysPending":3}""";

    private static readonly Guid SampleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Reader = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void WriteOneNotificationTheSameWayOverBothTransports()
    {
        using var metadata = JsonDocument.Parse(BusMetadata);

        // What the hub is handed: System.Text.Json read the object-typed property
        // off the bus back as a JsonElement, and the bus writes camelCase.
        var pushed = new Notification
        {
            Id = SampleId,
            EventType = EventType.NewMessage,
            Payload = metadata.RootElement.Clone()
        };

        // The second arm, a dictionary under CLR member names, keeps the converter normalizing.
        var stored = new Notification
        {
            Id = SampleId,
            EventType = EventType.NewMessage,
            Payload = new Dictionary<string, object>
            {
                ["GameTitle"] = "Frozen Keep",
                ["DaysPending"] = 3
            }
        };

        var api = ApiContract;
        var hub = HubContract;

        var wires = new[]
        {
            JsonSerializer.Serialize(pushed, api),
            JsonSerializer.Serialize(stored, api),
            JsonSerializer.Serialize(pushed, hub),
            JsonSerializer.Serialize(stored, hub)
        };

        wires.Distinct().Should().ContainSingle(
            "the list and the push reach the same open tab with the same notification, " +
            "and a screen can be written against one spelling only. Wires: {0}",
            string.Join(" | ", wires.Distinct()));

        // Guards against the pair agreeing on the wrong format: symmetric numbers
        // and symmetric CLR spelling would satisfy the equality above.
        wires[0].Should().Contain("\"eventType\":\"NewMessage\"",
            "the event travels as its name, the way every other enum this API writes does");
        wires[0].Should().Contain("\"gameTitle\"",
            "the metadata keys travel in the case the client reads them in");
    }

    // The pushed copy is taken from the processor itself, and the listed copy is handed the bag
    // under CLR member names.
    [Fact]
    public async Task PushTheRowTheListShows()
    {
        var createdUtc = new DateTimeOffset(2026, 10, 12, 11, 43, 0, TimeSpan.Zero);
        using var metadata = JsonDocument.Parse(
            """{"characterName":"Цири","gameTitle":"Ведьмак","gameId":"vedmak~aBcD","masterUsername":"SolohinLex"}""");

        var pushed = await PushAsync(new RealtimeNotification
        {
            NotificationId = SampleId,
            EventType = EventType.StatusCharacterAccepted,
            CreatedUtc = createdUtc,
            Metadata = metadata.RootElement.Clone(),
            RecipientIds = [Reader]
        });
        var listed = NotificationMapper.ToNotification(new ListedNotification
        {
            NotificationId = SampleId,
            EventType = EventType.StatusCharacterAccepted,
            CreatedUtc = createdUtc,
            Metadata = new Dictionary<string, object>
            {
                ["CharacterName"] = "Цири",
                ["GameTitle"] = "Ведьмак",
                ["GameId"] = "vedmak~aBcD",
                ["MasterUsername"] = "SolohinLex"
            },
            IsRead = false
        });

        // Compared as parsed: the two serializers escape a non-ASCII letter differently.
        var wires = new[]
        {
            JsonSerializer.Serialize(pushed, ApiContract),
            JsonSerializer.Serialize(listed, ApiContract),
            JsonSerializer.Serialize(pushed, HubContract),
            JsonSerializer.Serialize(listed, HubContract)
        }.Select(Parsed).ToArray();

        wires.Distinct().Should().ContainSingle(
            "the push and the row of the list are one notification on one open tab. Wires: {0}",
            string.Join(" | ", wires.Distinct()));

        // Guards against the pair agreeing on nothing: two empty titles and two
        // missing dates would satisfy the equality above.
        using var wire = JsonDocument.Parse(wires[2]);
        var row = wire.RootElement;
        row.GetProperty("title").GetString().Should().Be("Персонаж принят");
        row.GetProperty("url").GetString().Should().Be("/game/vedmak~aBcD");
        row.GetProperty("createdUtc").GetDateTimeOffset().Should().Be(createdUtc);
        row.GetProperty("isRead").GetBoolean().Should().BeFalse();
        row.GetProperty("details").EnumerateArray()
            .Select(detail => $"{detail.GetProperty("label").GetString()}: {detail.GetProperty("value").GetString()}")
            .Should().Equal("Персонаж: Цири", "Игра: Ведьмак", "Мастер: SolohinLex");
    }

    private static JsonSerializerOptions ApiContract
    {
        get
        {
            var options = new MvcJsonOptions();
            options.Setup(new HttpContextAccessor(), new BbParserProvider());
            return options.JsonSerializerOptions;
        }
    }

    private static JsonSerializerOptions HubContract =>
        new ServiceCollection()
            .AddDmSignalR()
            .BuildServiceProvider()
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value
            .PayloadSerializerOptions;

    /// <summary>
    /// A wire as the reader parses it, written back with every letter as itself.
    /// </summary>
    private static string Parsed(string wire) =>
        JsonNode.Parse(wire)!.ToJsonString(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    private static async Task<Notification> PushAsync(RealtimeNotification message)
    {
        Notification? sent = null;
        var reader = Stand.Of<INotificationHub>((_, args) =>
        {
            sent = (Notification)args[0]!;
            return Task.CompletedTask;
        });
        var clients = Stand.Of<IHubClients<INotificationHub>>((_, _) => reader);
        var hub = Stand.Of<IHubContext<NotificationHub, INotificationHub>>((_, _) => clients);
        var connections = Stand.Of<IUserConnectionService>((_, _) =>
            new Dictionary<Guid, IEnumerable<string>> { [Reader] = ["reader-connection"] });

        await new RealtimeNotificationProcessor(
                NullLogger<RealtimeNotificationProcessor>.Instance, connections, hub)
            .Process("routing-key", message, CancellationToken.None);

        sent.Should().NotBeNull("the reader is connected and the notification is addressed to them");
        return sent!;
    }

    public class Stand : DispatchProxy
    {
        private Func<MethodInfo, object?[], object?> _answer = (_, _) => null;

        public static T Of<T>(Func<MethodInfo, object?[], object?> answer) where T : class
        {
            var proxy = Create<T, Stand>();
            ((Stand)(object)proxy)._answer = answer;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            _answer(targetMethod!, args ?? []);
    }
}
