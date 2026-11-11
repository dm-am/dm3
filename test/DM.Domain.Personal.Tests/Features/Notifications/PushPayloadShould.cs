using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

/// <remarks>
/// A database notification carries at most 8000 bytes, so a push is cut into pieces the
/// listener reads back. What the listener reads is the proof, not how the pieces were cut.
/// </remarks>
public class PushPayloadShould
{
    private const int NotificationLimit = 7000;

    /// <remarks>Mutation it must fail on: a cut that drops the odd recipient of a halving.</remarks>
    [Fact]
    public void ReachEachOfAThousandRecipientsExactlyOnceUnderTheLimit()
    {
        var recipients = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray();

        var pieces = PushPayload.Cut(Push(recipients, new { gameTitle = "Frozen Keep" }));

        pieces.Should().AllSatisfy(piece => Encoding.UTF8.GetByteCount(piece).Should()
            .BeLessThan(NotificationLimit, "a notification past 8000 bytes is refused by the database"));
        pieces.Select(Read).SelectMany(push => push.RecipientIds).Should().BeEquivalentTo(recipients,
            options => options.WithoutStrictOrdering(),
            "each recipient is in exactly one piece, so nobody is missed and nobody hears it twice");
    }

    /// <remarks>
    /// The broadcast types of the global chat travel this way: addressed to nobody, heard by all.
    /// Mutation it must fail on: a cut that sends nothing for a push without recipients.
    /// </remarks>
    [Fact]
    public void GoAsOneNotificationWhenItHasNoRecipients()
    {
        var pieces = PushPayload.Cut(Push([], new { text = "hello" }));

        pieces.Should().ContainSingle("a push addressed to nobody is still one push for every open tab");
        Read(pieces[0]).RecipientIds.Should().BeEmpty();
    }

    /// <remarks>Mutation it must fail on: a piece of one recipient sent with its metadata past the limit.</remarks>
    [Fact]
    public void GoWithoutItsMetadataWhenOneRecipientDoesNotFit()
    {
        var recipient = Guid.NewGuid();

        var pieces = PushPayload.Cut(Push([recipient], new { text = new string('x', 8000) }));

        var piece = pieces.Should().ContainSingle().Subject;
        Encoding.UTF8.GetByteCount(piece).Should().BeLessThan(NotificationLimit,
            "a piece the database refuses reaches nobody, while one without metadata still moves the counter");
        var push = Read(piece);
        push.RecipientIds.Should().Equal(recipient);
        push.Metadata.Should().BeNull("the metadata was what did not fit");
    }

    /// <remarks>
    /// Only the application role can send on the channel, and still nothing but the site's
    /// own shape may reach an open tab. Mutation it must fail on: a read that lets members the
    /// push does not have through.
    /// </remarks>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{"hello":"world"}""")]
    [InlineData("""{"notificationId":"00000000-0000-0000-0000-000000000000","eventType":101,"recipientIds":[]}""")]
    [InlineData("""{"notificationId":"9b1c2f9e-1111-4f5e-9a39-8a2b6c0d1e2f","eventType":987654,"recipientIds":[]}""")]
    [InlineData("""{"notificationId":"9b1c2f9e-1111-4f5e-9a39-8a2b6c0d1e2f","eventType":101,"recipientIds":"everyone"}""")]
    [InlineData("""{"notificationId":"9b1c2f9e-1111-4f5e-9a39-8a2b6c0d1e2f","eventType":101,"recipientIds":[],"script":"x"}""")]
    public void RefuseANotificationOfAForeignShape(string payload) =>
        PushPayload.TryRead(payload, out _).Should().BeFalse("a tab only ever shows what the site produced");

    /// <remarks>Mutation it must fail on: a read that refuses a push without metadata, the shape of a piece cut without it.</remarks>
    [Fact]
    public void AcceptAPushWithEmptyMetadata()
    {
        var push = Push([Guid.NewGuid()], null);

        Read(PushPayload.Cut(push).Single()).NotificationId.Should().Be(push.NotificationId,
            "a push cut without its metadata is still the site's own");
    }

    private static RealtimeNotification Push(IEnumerable<Guid> recipients, object? metadata) => new()
    {
        NotificationId = Guid.NewGuid(),
        EventType = EventType.NewTopic,
        CreatedUtc = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
        RecipientIds = recipients.ToArray(),
        Metadata = metadata!
    };

    private static RealtimeNotification Read(string piece)
    {
        PushPayload.TryRead(piece, out var push).Should().BeTrue("the producer writes the shape the listener reads");
        return push!;
    }
}
