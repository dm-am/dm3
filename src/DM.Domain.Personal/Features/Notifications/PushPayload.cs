using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// The text a push travels as on the database's notification channel, written by the
/// notification worker and read by the listener of every API instance.
/// </summary>
/// <remarks>
/// The database refuses a notification of 8000 bytes or more, so a push is cut by its
/// recipients into pieces under <see cref="Limit"/>, each recipient in exactly one piece.
/// </remarks>
public static class PushPayload
{
    /// <summary>Bytes of UTF-8 every piece stays under, short of the database's 8000.</summary>
    public const int Limit = 7000;

    private static readonly JsonSerializerOptions Codec = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Cuts a push into the pieces it is sent as.
    /// </summary>
    /// <param name="push">The push with all its recipients.</param>
    public static IReadOnlyList<string> Cut(RealtimeNotification push)
    {
        var pieces = new List<string>();
        CutInto(pieces, push, push.RecipientIds.ToArray());
        return pieces;
    }

    // Halving until a piece fits: every recipient lands in exactly one half.
    private static void CutInto(List<string> pieces, RealtimeNotification push, Guid[] recipients)
    {
        var piece = Write(push, recipients);
        if (Fits(piece))
        {
            pieces.Add(piece);
            return;
        }

        if (recipients.Length > 1)
        {
            var half = recipients.Length / 2;
            CutInto(pieces, push, recipients[..half]);
            CutInto(pieces, push, recipients[half..]);
            return;
        }

        // Even one recipient does not fit: the metadata is what is too long, and a push
        // without it still moves the counter of an open tab.
        pieces.Add(Write(push, recipients, withMetadata: false));
    }

    private static string Write(RealtimeNotification push, Guid[] recipients, bool withMetadata = true) =>
        JsonSerializer.Serialize(new RealtimeNotification
        {
            NotificationId = push.NotificationId,
            EventType = push.EventType,
            CreatedUtc = push.CreatedUtc,
            Metadata = withMetadata ? push.Metadata : null!,
            RecipientIds = recipients
        }, Codec);

    private static bool Fits(string piece) => System.Text.Encoding.UTF8.GetByteCount(piece) < Limit;

    /// <summary>
    /// Reads one piece back into a push.
    /// </summary>
    /// <param name="payload">Text of the notification.</param>
    /// <param name="push">The push, when the text has its shape.</param>
    /// <remarks>
    /// Strict: a member the push does not have, an event type this build does not know or a
    /// push without an identifier is a foreign shape. Empty metadata is the site's own, the
    /// shape of a piece that did not fit with it.
    /// </remarks>
    public static bool TryRead(string payload, out RealtimeNotification? push)
    {
        push = null;
        RealtimeNotification? read;
        try
        {
            read = JsonSerializer.Deserialize<RealtimeNotification>(payload, StrictCodec);
        }
        catch (JsonException)
        {
            return false;
        }

        if (read is null || read.NotificationId == Guid.Empty || !Enum.IsDefined(read.EventType) ||
            read.RecipientIds is null)
        {
            return false;
        }

        push = read;
        return true;
    }

    private static readonly JsonSerializerOptions StrictCodec = new(Codec)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
}
