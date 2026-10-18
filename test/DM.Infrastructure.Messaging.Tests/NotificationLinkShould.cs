using System;
using System.Collections.Generic;
using System.Text.Json;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Email;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// A letter and a bot message carry the way back to what they are about.
/// </summary>
/// <remarks>
/// The reader of a notification in the application is one click away from the game
/// or the topic it is about; the reader of the same notification by mail, in
/// Telegram or in Discord was given the name of the thing and left to go and find
/// it. All three take the address from one table now, and this is what says they
/// read it at all: the table can be right while none of the three consults it, and
/// nothing in the compiler would notice.
///
/// The identifier is written the way a generator writes it — a transliterated title,
/// a tilde, and the guid in base64 with its two URL-unsafe characters replaced —
/// because the address goes through URL escaping on the way out, and escaping that
/// touches any of those produces a link resolving to nothing.
/// </remarks>
public class NotificationLinkShould
{
    private const string PublicUrl = "https://example.test";

    /// <summary>A game identifier as EncodeToReadable writes one.</summary>
    private const string GameIdentifier = "poterjannye-hroniki~aBcD-1_2";

    private static readonly SiteAddressConfiguration Addresses = new() { PublicUrl = PublicUrl };

    /// <summary>The payload of a game notification, identifier and all.</summary>
    private static readonly object GameMetadata = new
    {
        GameTitle = "Потерянные хроники",
        GameId = GameIdentifier,
        MasterUsername = "Аллигатор"
    };

    /// <summary>The same notification with nothing in it to point at.</summary>
    private static readonly object TitleOnlyMetadata = new
    {
        GameTitle = "Потерянные хроники",
        MasterUsername = "Аллигатор"
    };

    private static readonly string GameUrl = $"{PublicUrl}/game/{GameIdentifier}";

    [Fact]
    public void PointAtTheGameANotificationIsAbout() =>
        NotificationLink.GetUrl(EventType.StatusGameFrozen, GameMetadata, Addresses)
            .Should().Be(GameUrl,
                "the identifier is carried in the payload for exactly this, and escaping " +
                "that touched the tilde or the base64 alphabet would build a link to nothing");

    [Fact]
    public void PointAtTheRosterWhenACharacterIsApplied() =>
        NotificationLink.GetUrl(EventType.NewCharacter, GameMetadata, Addresses)
            .Should().Be($"{PublicUrl}/game/{GameIdentifier}/characters",
                "a master told about an application is being asked to answer it");

    [Fact]
    public void JoinTheAddressAndThePathWithOneSlash() =>
        NotificationLink.GetUrl(
                EventType.StatusGameFrozen,
                GameMetadata,
                new SiteAddressConfiguration { PublicUrl = $"{PublicUrl}/" })
            .Should().Be(GameUrl, "a deployment writes its own address either way");

    [Fact]
    public void PointNowhereWithoutAnIdentifier() =>
        NotificationLink.GetUrl(EventType.StatusGameFrozen, TitleOnlyMetadata, Addresses)
            .Should().BeNull("a link built without the identifier lands on the front page");

    [Fact]
    public void PointNowhereForAnEventWithNothingToPointAt() =>
        NotificationLink.GetUrl(EventType.PasswordChanged, GameMetadata, Addresses)
            .Should().BeNull("a changed password is not a page");

    [Fact]
    public void PointNowhereWithoutAPublicAddress() =>
        NotificationLink.GetUrl(
                EventType.StatusGameFrozen, GameMetadata, new SiteAddressConfiguration())
            .Should().BeNull("a path resolves against nothing in a letter");

    /// <summary>
    /// The address a channel sends is the root of the site in front of the path the
    /// list in the application is given, for every event, whichever spelling the bag
    /// arrives in.
    /// </summary>
    /// <remarks>
    /// The table of paths is shared with the API, and this is what says the
    /// dispatcher reads it rather than a copy of its own: a copy can agree with it
    /// row for row today and still answer the stored spelling of the bag, gameId,
    /// with nothing.
    /// </remarks>
    [Fact]
    public void TakeEveryPathFromTheSharedTable()
    {
        var written = new
        {
            GameId = GameIdentifier,
            TopicId = "obsuzhdenie~eFgH-3_4",
            BlogId = "zapiski~iJkL-5_6",
            PublicationId = "pervaja~mNoP-7_8",
            Username = "Player-Two",
            TicketId = "2f1c9a8e-4b7d-4e3a-9c1f-0a2b3c4d5e6f"
        };
        var stored = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(written, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var drift = new List<string>();
        var linked = 0;
        foreach (var eventType in Enum.GetValues<EventType>())
        {
            foreach (var bag in new object[] { written, stored })
            {
                var path = NotificationDestination.GetPath(eventType, bag);
                var expected = path == null ? null : PublicUrl + path;
                var sent = NotificationLink.GetUrl(eventType, bag, Addresses);
                if (sent != expected)
                {
                    drift.Add($"{eventType} from {bag.GetType().Name}: " +
                              $"{expected ?? "nothing"} in the list, {sent ?? "nothing"} in the letter");
                }

                if (path != null)
                {
                    linked++;
                }
            }
        }

        linked.Should().BeGreaterThan(80,
            "a rule that matches nothing passes: most events lead somewhere, in both spellings");
        drift.Should().BeEmpty(
            "the list and the letter reach one person minutes apart, and a destination " +
            "that differs between them sends him to two places for one event");
    }

    [Fact]
    public void PutTheLinkIntoTheLetter() =>
        NotificationEmailSender.BuildEmailBody(EventType.StatusGameFrozen, GameMetadata, Addresses)
            .Should().Contain($"<a href=\"{GameUrl}\">Перейти</a>",
                "the reader of a letter is the one reader with no notification list open");

    [Fact]
    public void PutTheLinkIntoTheTelegramMessage() =>
        NotificationBotSender.BuildTelegramMessage(EventType.StatusGameFrozen, GameMetadata, Addresses)
            .Should().Contain($"<a href=\"{GameUrl}\">Перейти</a>",
                "an anchor is the only way a message in this channel carries a link");

    [Fact]
    public void PutTheBareUrlIntoTheDiscordMessage()
    {
        var message = NotificationBotSender.BuildDiscordMessage(
            EventType.StatusGameFrozen, GameMetadata, Addresses);

        message.Should().Contain(GameUrl, "Discord links a URL of its own accord");
        message.Should().NotContain("<a ", "and prints an anchor with the tag showing");
    }

    [Fact]
    public void LeaveTheThreeChannelsWithoutALinkWhenThereIsNothingToPointAt()
    {
        var letter = NotificationEmailSender.BuildEmailBody(
            EventType.PasswordChanged, GameMetadata, Addresses);
        var telegram = NotificationBotSender.BuildTelegramMessage(
            EventType.PasswordChanged, GameMetadata, Addresses);
        var discord = NotificationBotSender.BuildDiscordMessage(
            EventType.PasswordChanged, GameMetadata, Addresses);

        letter.Should().NotContain("Перейти",
            "an offer to go somewhere is worth nothing when there is nowhere to go");
        telegram.Should().NotContain("<a ");
        discord.Should().NotContain(GameUrl);
    }
}
