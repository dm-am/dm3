using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Personal.Features.Notifications;
using DM.Tools.Seeder.Seeding;
using DM.Web.API.Features.Personal.Notifications;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The notifications the seed writes are rows the list can tell: a title of their
/// own, a link to the page the seed wrote, a place for "Где" and a person for "Кто".
/// </summary>
/// <remarks>
/// Nothing fails on a payload of another shape: the link is not built and the columns stay empty. Each
/// payload goes through the list's mapper and is held to the generator it copies.
/// </remarks>
public class SeededNotificationsShould
{
    /// <summary>What a row with no title of its own is headed, by NotificationWording.</summary>
    private const string Untitled = "Уведомление";

    /// <summary>The assignment every generator writes its payload through.</summary>
    private const string PayloadAssignment = "Metadata = new";

    private static readonly DateTimeOffset Epoch = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid TopicId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayedGameId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid RecruitingGameId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid BlogId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid LikedTopicId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid NewGameId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid LikedGameId = Guid.Parse("10000000-0000-0000-0000-000000000007");

    /// <summary>Every fact the seed reads, with the names and titles of the fixture.</summary>
    private static readonly DataSeeder.NotificationFacts Everything = new(
        new DataSeeder.CommentFact(
            Guid.Parse("20000000-0000-0000-0000-000000000001"), TopicId, "FAQ для новых пользователей", "Player Four"),
        new DataSeeder.CharacterFact(
            Guid.Parse("20000000-0000-0000-0000-000000000002"), "Цири", PlayedGameId,
            "Хроники Забытых Королевств", "TestMentor"),
        new DataSeeder.GameFact(RecruitingGameId, "Врата Бездны", "SolohinLex"),
        new DataSeeder.BlogFact(BlogId, "Записки мастера", "TestHonorary"),
        new DataSeeder.LikeFact(LikedTopicId, "Обсуждение", "Ян"),
        new DataSeeder.GameFact(NewGameId, "Сага о Викингах", "Игрок_Один"),
        new DataSeeder.LikeFact(LikedGameId, "Пески Времени", "Тест Елки"));

    /// <summary>
    /// A row as the list has to show it: where the title leads, and the details as
    /// "Label: value" in the column each of them stands in.
    /// </summary>
    private sealed record Row(string Path, Guid Page, string Place, string Person, string[] Rest);

    private static readonly IReadOnlyDictionary<EventType, Row> Expected = new Dictionary<EventType, Row>
    {
        [EventType.NewCommentInSubscribedTopic] = new(
            "/forum-topic/", TopicId, "Топик: FAQ для новых пользователей", "Автор: Player Four", []),
        [EventType.StatusCharacterAccepted] = new(
            "/game/", PlayedGameId, "Игра: Хроники Забытых Королевств", "Мастер: TestMentor", ["Персонаж: Цири"]),
        [EventType.GameRecruitmentOpened] = new(
            "/game/", RecruitingGameId, "Игра: Врата Бездны", "Мастер: SolohinLex", []),
        [EventType.NewBlogFromSubscribedAuthor] = new(
            "/blogs/", BlogId, "Блог: Записки мастера", "Автор: TestHonorary", []),
        [EventType.LikedTopicComment] = new(
            "/forum-topic/", LikedTopicId, "Топик: Обсуждение", "Отметил: Ян", []),
        [EventType.NewGameFromSubscribedAuthor] = new(
            "/game/", NewGameId, "Игра: Сага о Викингах", "Автор: Игрок_Один", []),
        [EventType.LikedGameComment] = new(
            "/game/", LikedGameId, "Игра: Пески Времени", "Отметил: Тест Елки", []),
    };

    /// <summary>The titles the list sorts into "Где" (parts.ts, PLACES).</summary>
    private static readonly string[] PlaceFields =
        ["gameTitle", "roomTitle", "topicTitle", "boardTitle", "blogTitle", "publicationTitle"];

    /// <summary>
    /// Each row reads as the list shows it: a title of its own, a link to the
    /// seeded page, and exactly a place, a person and nothing else unaccounted for.
    /// </summary>
    /// <remarks>Read in both key orders: jsonb does not keep the order the list pairs details by.</remarks>
    [Fact]
    public void ReadAsTheRowsOfTheList()
    {
        var seeded = DataSeeder.NotificationsAbout(Everything, Epoch);
        seeded.Select(n => n.EventType).Should().BeEquivalentTo(Expected.Keys,
            "every fact the seed reads is written as a row, and each row is described here");

        foreach (var notification in seeded)
        {
            var expected = Expected[notification.EventType];
            foreach (var (order, payload) in new[]
                     {
                         ("in the order written", notification.Metadata),
                         ("in the reverse order", Reversed(notification.Metadata))
                     })
            {
                var row = AsListed(notification, payload);
                var because = $"{notification.EventType}, its fields {order}";

                row.Title.Should().NotBeNullOrEmpty(because).And.NotBe(Untitled, because);
                row.Url.Should().StartWith(expected.Path, because);
                Uri.UnescapeDataString(row.Url![expected.Path.Length..]).DecodeFromReadableGuid()
                    .Should().Be(expected.Page, "the title leads to the page the seed wrote: " + because);

                var (places, people, rest) = Columns(row);
                places.Should().Equal(new[] { expected.Place }, "the column \"Где\": " + because);
                people.Should().Equal(new[] { expected.Person }, "the column \"Кто\": " + because);
                rest.Should().Equal(expected.Rest, "what stands under the title: " + because);
            }
        }
    }

    /// <summary>
    /// The line of what is unread stands over the list: the newest rows are unread,
    /// and they stay so when the fixture lacks the facts it may lack.
    /// </summary>
    [Fact]
    public void LeaveTheNewestUnread()
    {
        var seeded = DataSeeder.NotificationsAbout(Everything, Epoch);

        seeded.Should().HaveCount(7);
        seeded.Count(n => !n.IsRead).Should().Be(4);
        seeded.Should().BeInDescendingOrder(n => n.CreatedUtc, "the list stands newest first");
        seeded.Should().OnlyContain(n => n.CreatedUtc < Epoch, "the seed writes no future");
        seeded.TakeWhile(n => !n.IsRead).Should().HaveCount(4,
            "the unread are the newest, as in a list nobody has opened for a day");

        // The reader's character and his liked comment are a draw of the seed.
        var drawn = Everything with { AcceptedCharacter = null, TopicCommentLike = null, GameCommentLike = null };
        DataSeeder.NotificationsAbout(drawn, Epoch).Count(n => !n.IsRead).Should().Be(3,
            "the facts a fixture may lack cost their rows and not the line of what is unread");
    }

    /// <summary>
    /// Under the seven, the read rows of the weeks before: enough for the list to run past a page of
    /// twenty, each a row of its own, older than the seven and carrying what its generator writes.
    /// </summary>
    [Fact]
    public void RunPastAPageOfTwentyWithTheEarlierRows()
    {
        var comments = Enumerable.Range(1, DataSeeder.EarlierNotificationCount)
            .Select(n => new DataSeeder.CommentFact(
                Guid.Parse($"40000000-0000-0000-0000-{n:x12}"), Guid.Parse($"50000000-0000-0000-0000-{n:x12}"),
                $"Топик {n}", "Ян"))
            .ToList();
        var newest = DataSeeder.NotificationsAbout(Everything, Epoch);
        var earlier = DataSeeder.EarlierNotificationsAbout(comments, Epoch);

        newest.Concat(earlier).Should().HaveCountGreaterThan(20,
            "the list then takes two pages at twenty rows a page, so the size of its page shows on the stand");
        earlier.Should().OnlyContain(n => n.IsRead, "the line of what is unread stays the seven's");
        earlier.Should().OnlyContain(n => n.CreatedUtc < newest.Min(x => x.CreatedUtc),
            "the list stands newest first, and these stand under the seven");
        earlier.Should().BeInDescendingOrder(n => n.CreatedUtc);

        var generated = GeneratorPayloads(EventType.NewCommentInSubscribedTopic);
        foreach (var (notification, comment) in earlier.Zip(comments))
        {
            var row = AsListed(notification, notification.Metadata);
            var because = $"the row about \"{comment.TopicTitle}\"";

            row.Title.Should().NotBeNullOrEmpty(because).And.NotBe(Untitled, because);
            row.Url.Should().StartWith("/forum-topic/", because);
            Uri.UnescapeDataString(row.Url!["/forum-topic/".Length..]).DecodeFromReadableGuid()
                .Should().Be(comment.TopicId, "the title leads to the topic the comment stands in: " + because);

            var (places, people, rest) = Columns(row);
            places.Should().Equal(new[] { $"Топик: {comment.TopicTitle}" }, because);
            people.Should().Equal(new[] { "Автор: Ян" }, because);
            rest.Should().BeEmpty(because);

            using var payload = JsonDocument.Parse(notification.Metadata);
            var keys = payload.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant());
            foreach (var generatorKeys in generated)
            {
                keys.Should().BeEquivalentTo(generatorKeys.Select(k => k.ToLowerInvariant()), because);
            }
        }
    }

    /// <summary>
    /// Each payload carries the fields its generator writes, no more and no fewer.
    /// </summary>
    /// <remarks>Every generator that writes the event is asked: a new game reaches the subscribers from two.</remarks>
    [Fact]
    public void CarryThePayloadItsGeneratorWrites()
    {
        foreach (var notification in DataSeeder.NotificationsAbout(Everything, Epoch))
        {
            using var payload = JsonDocument.Parse(notification.Metadata);
            var seededKeys = payload.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant());

            var generated = GeneratorPayloads(notification.EventType);
            generated.Should().NotBeEmpty($"a generator writes {notification.EventType}");
            foreach (var generatorKeys in generated)
            {
                seededKeys.Should().BeEquivalentTo(generatorKeys.Select(k => k.ToLowerInvariant()),
                    $"the seeded {notification.EventType} carries what its generator writes");
            }
        }
    }

    /// <summary>
    /// The row the list is answered with, from the payload as the store hands it
    /// back: a document, not a string.
    /// </summary>
    private static Notification AsListed(DataSeeder.SeededNotification notification, string payload) =>
        NotificationMapper.ToNotification(new ListedNotification
        {
            NotificationId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            EventType = notification.EventType,
            CreatedUtc = notification.CreatedUtc,
            Metadata = JsonSerializer.Deserialize<JsonElement>(payload),
            IsRead = notification.IsRead
        });

    /// <summary>The same payload with its keys in the reverse order.</summary>
    private static string Reversed(string payload)
    {
        var fields = JsonNode.Parse(payload)!.AsObject()
            .Select(field => KeyValuePair.Create(field.Key, field.Value?.DeepClone()))
            .Reverse();
        return new JsonObject(fields).ToJsonString();
    }

    /// <summary>
    /// The details of a row sorted into the columns of the list, the way
    /// src/DM.Web.Client/src/entities/notification/lib/parts.ts sorts them.
    /// </summary>
    private static (List<string> Places, List<string> People, List<string> Others) Columns(Notification row)
    {
        var fields = ((JsonElement)row.Payload).EnumerateObject()
            .Where(field => !field.Name.Equals("username", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var columns = (Places: new List<string>(), People: new List<string>(), Others: new List<string>());

        var next = 0;
        foreach (var detail in row.Details)
        {
            var column = columns.Others;
            for (var i = next; i < fields.Count; i++)
            {
                if (NotificationWording.FormatPropertyValue(fields[i].Value) != detail.Value)
                {
                    continue;
                }

                var name = fields[i].Name;
                if (name.EndsWith("username", StringComparison.OrdinalIgnoreCase))
                {
                    column = columns.People;
                }
                else if (PlaceFields.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    column = columns.Places;
                }

                next = i + 1;
                break;
            }

            column.Add($"{detail.Label}: {detail.Value}");
        }

        return columns;
    }

    /// <summary>
    /// The members of every payload a generator of the dispatcher writes for the
    /// event, read out of the sources the way NotificationMetadataShould reads them.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> GeneratorPayloads(EventType eventType)
    {
        var mention = new Regex($@"\bEventType\.{eventType}\b");
        var payloads = new List<IReadOnlyList<string>>();
        foreach (var file in Directory.GetFiles(NotifiersDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var code = SourceText.ReadCode(file);
            if (!mention.IsMatch(code))
            {
                continue;
            }

            for (var at = code.IndexOf(PayloadAssignment, StringComparison.Ordinal);
                 at >= 0;
                 at = code.IndexOf(PayloadAssignment, at + 1, StringComparison.Ordinal))
            {
                var opening = code.IndexOf('{', at);
                var closing = ObjectInitializer.ClosingBrace(code, opening);
                payloads.Add(ObjectInitializer.Members(code[(opening + 1)..closing]).ToList());
            }
        }

        return payloads;
    }

    private static string NotifiersDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Workers.NotificationDispatcher", "Notifiers");
}
