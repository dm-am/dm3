using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Shared.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
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
/// metadata bag arrived camelCase from the bus but PascalCase from Mongo,
/// because a dictionary key is not touched by PropertyNamingPolicy. A screen can
/// only be written against one of the two, and both were on the same page.
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

        // What the list is handed: Mongo stores the CLR member names of the same
        // metadata object, so it comes back PascalCase.
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
}
