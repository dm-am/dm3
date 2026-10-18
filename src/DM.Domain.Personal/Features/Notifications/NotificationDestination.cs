using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Where a notification points, as a path from the root of the site, for every
/// reader of one: the letter, the two bots and the list in the application.
/// </summary>
/// <remarks>
/// The list in the application had a table of these from the start and the server
/// had none at all, so a letter and both bot messages named what had happened and
/// led to the front page. The server table that closed that gap lived in the
/// dispatcher, which left the list on a narrower copy of its own; it lives here
/// now, so that the dispatcher and the API read one table.
///
/// The identifier is read out of the payload field the generator wrote it in.
/// Which is what those fields are carried for: NotificationWording keeps them out
/// of the sentence because a slug is no use to a person, and this is the reader
/// they stayed in the bag for.
///
/// A path and not an address. The root is the address a deployment answers on,
/// which is configuration, and only a reader with nothing to resolve a path
/// against needs it: the dispatcher puts it in front for a letter and a bot
/// message, the list in the application resolves the path against the site it is
/// on.
///
/// An event with no destination and a payload with no identifier both yield no
/// path rather than the front page. A changed password is not a page, and a link
/// that lands on the front page is a promise the notification does not keep.
/// </remarks>
public static class NotificationDestination
{
    /// <summary>What a path carries in place of the identifier.</summary>
    private const string Placeholder = "{id}";

    /// <summary>
    /// A destination: the payload field the identifier is read from, and the path it
    /// goes into.
    /// </summary>
    private readonly record struct Destination(string Field, string Path);

    /// <summary>
    /// Path from the root of the site to what a notification is about, or null when
    /// there is nothing to point at.
    /// </summary>
    /// <param name="eventType">Event the notification is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    public static string? GetPath(EventType eventType, object? metadata)
    {
        var destination = DestinationOf(eventType);
        if (destination == null)
        {
            return null;
        }

        var identifier = Identifier(metadata, destination.Value.Field);
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        return destination.Value.Path.Replace(Placeholder, Uri.EscapeDataString(identifier));
    }

    /// <summary>
    /// The payload field the path of an event is built from, or null for an event
    /// that leads nowhere.
    /// </summary>
    /// <remarks>
    /// The other half of the contract with the generators: a notification that
    /// carries one of these events and not its field is text nobody can follow, and
    /// NotificationLinkShould in DM.Architecture.Tests asks every generator for it.
    /// </remarks>
    /// <param name="eventType">Event the notification is about</param>
    public static string? FieldOf(EventType eventType) => DestinationOf(eventType)?.Field;

    /// <summary>
    /// Where an event leads, or null for an event that leads nowhere.
    /// </summary>
    /// <remarks>
    /// A switch answering with a pair, rather than a dictionary keyed by event: the
    /// one table of that shape the notification readers hold is the wording, and
    /// NotificationWordingShould counts them to keep it the only one.
    /// </remarks>
    private static Destination? DestinationOf(EventType eventType) => eventType switch
    {
        // A publication. It has a page of its own now, so what is written about one
        // publication leads there rather than to the whole feed. The address is the
        // resolver route and not /blogs/{blog}/feed/{pub}: the metadata names both in
        // the readable-guid form, which the publication endpoint takes and the blog
        // endpoint does not.
        EventType.NewPublication or EventType.ChangedPublication or
        EventType.LikedPublication or EventType.LikedPublicationComment or
        EventType.NewPublicationComment
            => new Destination("PublicationId", "/publication/{id}"),

        // The blog. What is written about the blog itself leads to the blog.
        EventType.NewBlogComment or EventType.LikedBlogComment or
        EventType.BlogInvitationCreated or
        EventType.StatusBlogActive or EventType.StatusBlogClosed or
        EventType.StatusBlogFrozen or EventType.StatusBlogFinished or
        EventType.NewBlogFromSubscribedAuthor
            => new Destination("BlogId", "/blogs/{id}"),

        // The topic. The canonical route is board alias and number, and the payload
        // carries neither, so the link goes through the resolver the interface keeps
        // for exactly this.
        EventType.ChangedTopic or EventType.LikedTopic or
        EventType.LikedTopicComment or
        EventType.NewCommentInSubscribedTopic or
        EventType.NewTopicFromSubscribedAuthor
            => new Destination("TopicId", "/forum-topic/{id}"),

        // The roster of a game: an application for a character is read there, and the
        // master told about one is being asked to answer it.
        EventType.NewCharacter => new Destination("GameId", "/game/{id}/characters"),

        // The warning log, narrowed to the reader it was issued to. The profile
        // shows a warning as points and a date; the reason it was issued for is on
        // the log and nowhere else, and the log is open to everyone. A corrected
        // warning leads to the same page: the second signature under it, with the
        // reason the points were changed, is written on the log too.
        EventType.WarningIssued or EventType.WarningPointsChanged
            => new Destination("Username", "/warnings?username={id}"),

        // The profile of the banned reader: the length of the ban stands there, and
        // the log carries warnings only.
        EventType.BanIssued => new Destination("Username", "/users/{id}"),

        // The page of the ticket its author reads, where the answer stands in the
        // conversation and the closing under it. Only an author with an account is
        // told, and that page is his.
        EventType.TicketAnswered or EventType.TicketResolved
            => new Destination("TicketId", "/my-tickets/{id}"),

        // The page of the ticket moderation reads: the reply is news to the
        // moderators in the conversation, and they answer it there. A ticket just
        // filed is told to the moderators only, and they take it up on that page.
        EventType.TicketReplied or EventType.TicketCreated
            => new Destination("TicketId", "/moderation/tickets/{id}"),

        // The hall of fame of the recipient's profile, where the award stands
        // with its series and the one who granted it. The profile opens on that
        // tab when the address names it.
        EventType.AwardGranted => new Destination("Username", "/users/{id}?tab=achievements"),

        // The game. A room is addressed by its number and the payload carries an
        // identifier, so a notification about an owed post leads to the game the post
        // is owed in.
        EventType.StatusGameActive or EventType.StatusGameClosed or
        EventType.StatusGameFrozen or EventType.StatusGameFinished or
        EventType.GameClosureWarning or EventType.GameInactivityWarning or
        EventType.GameRecruitmentOpened or EventType.LikedGameComment or
        EventType.AssignmentRequestCreated or
        EventType.PlayerInvitationCreated or EventType.ReaderInvitationCreated or
        EventType.RoomPendencyCreated or EventType.RoomPendencyFulfilled or
        EventType.RoomPendencyReminder or
        EventType.StatusCharacterAccepted or EventType.StatusCharacterDeclined or
        EventType.StatusCharacterDied or EventType.StatusCharacterResurrected or
        EventType.StatusCharacterReadmitted or
        EventType.StatusCharacterLeft or
        EventType.StatusCharacterExiled or EventType.StatusCharacterRetired or
        EventType.NewGameFromSubscribedAuthor
            => new Destination("GameId", "/game/{id}"),

        _ => null
    };

    /// <summary>
    /// The identifier a destination is built from, as the generator wrote it.
    /// </summary>
    /// <remarks>
    /// Read through the shared reader rather than a parse of its own: the bag was
    /// once read once per channel, and the copies differed down to their serializer
    /// options. Matched whatever its case: the generator writes GameId, the store
    /// hands the same bag back as gameId.
    /// </remarks>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="field">Name the generator declared the identifier by</param>
    private static string? Identifier(object? metadata, string field)
    {
        foreach (var (name, value) in NotificationWording.ReadFields(metadata))
        {
            if (string.Equals(name, field, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
