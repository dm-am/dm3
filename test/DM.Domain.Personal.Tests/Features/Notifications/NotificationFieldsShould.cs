using System.Collections.Generic;
using System.Text.Json;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

/// <summary>
/// A bag that cannot be read costs the notification its details, never its delivery.
/// </summary>
/// <remarks>
/// The store hands a bag back as a parsed element and a generator hands it over as
/// an object, so a malformed bag is one of the wrong kind or one the serializer
/// refuses, and none of them may escape the reader.
/// </remarks>
public class NotificationFieldsShould
{
    // Points at itself, which the serializer refuses as a cycle.
    private sealed class Loop
    {
        public Loop? Next { get; set; }
    }

    private static Loop SelfReferencing()
    {
        var loop = new Loop();
        loop.Next = loop;
        return loop;
    }

    public static TheoryData<string, object> Unreadable => new()
    {
        { "text that is not JSON", "{not json" },
        { "an array where an object is read", JsonDocument.Parse("[1, 2]").RootElement },
        { "a number where an object is read", JsonDocument.Parse("3").RootElement },
        { "an object with no property", JsonDocument.Parse("{}").RootElement },
        { "a type the serializer does not support", new { Kind = typeof(string) } },
        { "an object cycle", SelfReferencing() },
        { "an element that was never set", default(JsonElement) },
    };

    [Theory]
    [MemberData(nameof(Unreadable))]
    public void ReadNothingOutOfABagItCannotRead(string because, object metadata) =>
        NotificationWording.ReadFields(metadata).Should().BeEmpty(because);

    [Fact]
    public void ReadAPropertyOfAnotherTypeAsItsText() =>
        NotificationWording.ReadFields(JsonDocument.Parse("""{"GameId": 5, "Points": {"a": 1}}""").RootElement)
            .Should().Equal(
                new KeyValuePair<string, string>("GameId", "5"),
                new KeyValuePair<string, string>("Points", """{"a":1}"""));
}
