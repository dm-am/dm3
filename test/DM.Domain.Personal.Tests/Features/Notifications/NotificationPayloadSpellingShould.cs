using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

// A letter reads GameId from the generator, and whatever reads the stored bag back reads gameId.
public class NotificationPayloadSpellingShould
{
    private static readonly object Accepted = new
    {
        CharacterName = "Цири",
        GameTitle = "Ведьмак: Дикая Охота",
        GameId = "vedmak-dikaja-ohota~aBcD-1_2",
        MasterUsername = "SolohinLex",
        CreatedUtc = new DateTimeOffset(2026, 10, 12, 11, 43, 0, TimeSpan.Zero)
    };

    private static readonly object Identified = new
    {
        GameId = "vedmak-dikaja-ohota~aBcD-1_2",
        TopicId = "obsuzhdenie~eFgH-3_4",
        BlogId = "zapiski~iJkL-5_6",
        PublicationId = "pervaja~mNoP-7_8",
        Username = "Player-Two",
        TicketId = "2f1c9a8e-4b7d-4e3a-9c1f-0a2b3c4d5e6f"
    };

    [Fact]
    public void LeadEveryEventToTheSamePlace()
    {
        var stored = Stored(Identified);
        var linked = 0;

        foreach (var eventType in Enum.GetValues<EventType>())
        {
            var written = NotificationDestination.GetPath(eventType, Identified);
            NotificationDestination.GetPath(eventType, stored).Should().Be(written,
                $"{eventType} read back from the store is the same notification the generator wrote");

            if (written != null)
            {
                linked++;
            }
        }

        linked.Should().BeGreaterThan(40,
            "a rule that matches nothing passes: most events lead somewhere");
    }

    [Fact]
    public void LeadAnAcceptedCharacterToItsGameFromTheStore() =>
        NotificationDestination.GetPath(EventType.StatusCharacterAccepted, Stored(Accepted))
            .Should().Be("/game/vedmak-dikaja-ohota~aBcD-1_2",
                "the list reads the bag the store hands back, and the store spells it gameId");

    [Fact]
    public void ShowTheSameDetails()
    {
        var written = NotificationWording.ReadDetails(Accepted);

        written.Should().Equal(
            new NotificationDetail("Персонаж", "Цири", false),
            new NotificationDetail("Игра", "Ведьмак: Дикая Охота", false),
            new NotificationDetail("Мастер", "SolohinLex", false));
        NotificationWording.ReadDetails(Stored(Accepted)).Should().Equal(written,
            "an identifier is hidden and a label is found whichever way the key is spelled");
    }

    [Fact]
    public void WriteTheSameLetter() =>
        Lines(NotificationWording.ReadMetadata(Stored(Accepted)))
            .Should().Equal(Lines(NotificationWording.ReadMetadata(Accepted)),
                "a letter written from the stored bag says what the one from the generator says");

    [Fact]
    public void TitleTheEventWhateverItsBag() =>
        NotificationWording.GetTitle(EventType.StatusCharacterAccepted).Should().Be("Персонаж принят");

    /// <summary>
    /// The bag as the store hands it back: written with the web defaults, read as a
    /// document.
    /// </summary>
    private static JsonElement Stored(object written) => JsonSerializer.Deserialize<JsonElement>(
        JsonSerializer.Serialize(written, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static IEnumerable<string> Lines(IEnumerable<KeyValuePair<string, string>> fields) =>
        fields.Select(field => $"{field.Key}: {field.Value}");
}
