using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Web.API.Features.Personal.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

// The details follow the bag, and jsonb orders keys by length, then by bytes.
public class NotificationMapperShould
{
    private const string PublicationComment = """
        {"commentId":"c1","publicationId":"pervaja~mNoP","publicationTitle":"Первая партия",
         "blogId":"zapiski~iJkL","blogTitle":"Записки мастера","authorUsername":"TestHonorary"}
        """;

    private const string ChangedTopic = """
        {"topicId":"faq~eFgH","topicTitle":"FAQ","boardId":"obshchee~aBcD",
         "boardTitle":"Общее","authorUsername":"TestModerator"}
        """;

    public static TheoryData<EventType, string, string, string> TwoPlaces => new()
    {
        { EventType.NewPublicationComment, PublicationComment, "/publication/pervaja~mNoP", "Первая партия" },
        { EventType.ChangedTopic, ChangedTopic, "/forum-topic/faq~eFgH", "FAQ" }
    };

    [Theory]
    [MemberData(nameof(TwoPlaces))]
    public void LeadFromTheSamePlaceWhateverTheOrderOfTheBag(
        EventType eventType, string written, string path, string place)
    {
        foreach (var (order, bag) in new[]
                 {
                     ("as the generator wrote it", written),
                     ("as jsonb hands it back", Stored(written)),
                     ("reversed", Reversed(written))
                 })
        {
            var row = NotificationMapper.ToNotification(Listed(eventType, bag));

            row.Url.Should().Be(path, $"the link of {eventType} {order}");
            row.Details.Where(detail => detail.IsDestination).Select(detail => detail.Value)
                .Should().Equal(new[] { place }, $"the place the link of {eventType} {order} reads");
        }
    }

    private static ListedNotification Listed(EventType eventType, string bag) => new()
    {
        NotificationId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
        EventType = eventType,
        CreatedUtc = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
        Metadata = JsonSerializer.Deserialize<JsonElement>(bag),
        IsRead = false
    };

    // The bag in the order jsonb keeps its keys: the shorter first, and keys of one length by their
    // bytes (lengthCompareJsonbString in jsonb_util.c of PostgreSQL).
    private static string Stored(string bag) => Reordered(bag, fields => fields
        .OrderBy(field => field.Key.Length)
        .ThenBy(field => field.Key, StringComparer.Ordinal));

    private static string Reversed(string bag) => Reordered(bag, fields => fields.Reverse());

    private static string Reordered(
        string bag, Func<IEnumerable<KeyValuePair<string, JsonNode?>>, IEnumerable<KeyValuePair<string, JsonNode?>>> order)
    {
        var fields = JsonNode.Parse(bag)!.AsObject()
            .Select(field => KeyValuePair.Create(field.Key, field.Value?.DeepClone()));
        return new JsonObject(order(fields)).ToJsonString();
    }
}
