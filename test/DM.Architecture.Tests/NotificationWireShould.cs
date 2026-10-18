using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
/// One notification, one format, whichever transport carried it.
/// </summary>
/// <remarks>
/// The same DTO leaves the API twice: in the notification list over REST and in
/// a push over the hub. They were two contracts. MVC serializes an enum through
/// JsonStringEnumConverter while the hub sat on the protocol defaults, so the
/// event arrived as "NewMessage" from one and as 11 from the other. And the
/// metadata bag arrived camelCase from the bus but PascalCase from the store,
/// because a dictionary key is not touched by PropertyNamingPolicy. The store
/// writes the bus spelling since W1.1 (NotificationRepository.MetadataOptions),
/// and the dictionary arm below stands in for any writer that still hands the
/// bag over under CLR member names.
///
/// Nothing in either path fails when they disagree — a title is simply not found
/// and a link is simply not built — so the agreement has to be asserted rather
/// than observed.
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

        // What the list is handed today is a JsonElement parsed back from the
        // jsonb column, already in the bus spelling — the same shape as the
        // pushed copy. The dictionary under CLR member names is kept as the
        // second arm so the converter goes on normalizing a writer that hands
        // the bag over unserialized.
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

    /// <summary>
    /// The push carries the row the list shows: the title, the address, the
    /// details, the date and the mark, put together by the one mapper both
    /// transports call.
    /// </summary>
    /// <remarks>
    /// The hub built its DTO by hand next to the list's, field by field. A field
    /// added to one and not to the other arrives on the open tab as undefined
    /// over the socket and as a value over REST, and nothing fails. The push
    /// is unread by definition: it is the moment the notification appears.
    ///
    /// The pushed copy is taken from the processor itself, so a hub that went
    /// back to building the DTO on its own would be caught here, and the listed
    /// copy is handed the bag under CLR member names, so the words and the path
    /// are shown to be read whatever the spelling of the bag.
    /// </remarks>
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

        // Compared as the reader parses them. The two serializers escape a letter
        // outside ASCII each their own way, one as a numeric escape and one as
        // the letter, and the title is Russian: the same string, two spellings
        // of the same JSON.
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

    /// <summary>
    /// The host registers the hub through the shared entry point.
    /// </summary>
    /// <remarks>
    /// Source text is the only surface: the equality above proves what
    /// AddDmSignalR builds, and a Startup that stopped calling it would leave that
    /// proof about a registration nothing performs.
    /// </remarks>
    [Fact]
    public void RegisterTheHubThroughTheSharedContract()
    {
        var startup = File.ReadAllText(Path.Combine(SourceDirectory, "DM.Web.API", "Startup.cs"));

        startup.Should().Contain("AddDmSignalR()",
            "the hub takes the API JSON contract from the one place MVC takes it from");
        startup.Should().NotContain("services.AddSignalR()",
            "a bare AddSignalR leaves the hub on the protocol defaults, which write an " +
            "enum as its number");
    }

    /// <summary>What MVC answers a request with.</summary>
    private static JsonSerializerOptions ApiContract
    {
        get
        {
            var options = new MvcJsonOptions();
            options.Setup(new HttpContextAccessor(), new BbParserProvider());
            return options.JsonSerializerOptions;
        }
    }

    /// <summary>What the hub pushes with.</summary>
    private static JsonSerializerOptions HubContract =>
        new ServiceCollection()
            .AddDmSignalR()
            .BuildServiceProvider()
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value
            .PayloadSerializerOptions;

    private static string SourceDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    /// <summary>
    /// A wire as the reader parses it, written back with every letter as itself.
    /// </summary>
    private static string Parsed(string wire) =>
        JsonNode.Parse(wire)!.ToJsonString(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    /// <summary>
    /// What the hub sends the connections of the reader when the message reaches the API.
    /// </summary>
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

    /// <summary>
    /// Stands in for an interface of the host, answering every call the way the
    /// test says. Hand-written stand-ins would have to follow every member SignalR
    /// adds to its client and context interfaces.
    /// </summary>
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
