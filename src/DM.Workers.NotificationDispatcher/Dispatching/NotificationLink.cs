using System;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Where a notification points, for the three channels that reach a reader who is
/// not looking at the site.
/// </summary>
/// <remarks>
/// The list in the application has had a table of these from the start and the
/// server had none at all, so a letter and both bot messages named what had
/// happened and led to the front page. That is a gap between channels rather than a
/// broken feature, and this is the half that was missing: one table, read by the
/// letter, by Telegram and by Discord alike.
///
/// The destinations are the four the interface itself sends a notification to, and
/// the identifier is read out of the same payload field the list reads. Which is
/// what those fields are carried for: NotificationText keeps them out of the
/// sentence because a slug is no use to a person, and this is the reader they stayed
/// in the bag for.
///
/// Wider than the client's table, because that one covers what a screen links and
/// this one covers what a channel sends: a frozen game, an invitation, an owed post,
/// an accepted character are all letters, and every one of them is about a game its
/// reader has to be able to open. Where the two name the same event they answer the
/// same address, and NotificationLinkVocabularyShould in DM.Architecture.Tests is
/// what holds them to it.
///
/// An event with no destination and a payload with no identifier both yield no link
/// rather than a link to the front page. A changed password is not a page, and a
/// link that lands on the front page is a promise the letter does not keep.
/// </remarks>
internal static class NotificationLink
{
    /// <summary>What a path carries in place of the identifier.</summary>
    private const string Placeholder = "{id}";

    /// <summary>
    /// A destination: the payload field the identifier is read from, and the path it
    /// goes into.
    /// </summary>
    private readonly record struct Destination(string Field, string Path);

    /// <summary>
    /// Absolute address of what a notification is about, or null when there is
    /// nothing to point at.
    /// </summary>
    /// <remarks>
    /// Absolute, because a letter and a bot message resolve a path against nothing.
    /// The root is the address this deployment answers on, which is configuration
    /// rather than a constant: a link built on one address and read by somebody who
    /// reaches only the other one leads nowhere.
    /// </remarks>
    /// <param name="eventType">Event the notification is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    public static string? GetUrl(
        EventType eventType, object? metadata, SiteAddressConfiguration addresses)
    {
        var destination = DestinationOf(eventType);
        if (destination == null || string.IsNullOrWhiteSpace(addresses.PublicUrl))
        {
            return null;
        }

        var identifier = Identifier(metadata, destination.Value.Field);
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        var path = destination.Value.Path.Replace(Placeholder, Uri.EscapeDataString(identifier));
        return $"{addresses.PublicUrl.TrimEnd('/')}{path}";
    }

    /// <summary>
    /// Where an event leads, or null for an event that leads nowhere.
    /// </summary>
    /// <remarks>
    /// A switch answering with a pair, rather than a dictionary keyed by event: the
    /// one table of that shape this assembly holds is the wording, and
    /// NotificationWordingShould counts them to keep it the only one.
    /// </remarks>
    private static Destination? DestinationOf(EventType eventType) => eventType switch
    {
        // The blog. A publication has no page of its own — it is read in the feed of
        // the blog carrying it — so everything written in a blog leads to the blog.
        EventType.NewPublication or EventType.ChangedPublication or
        EventType.LikedPublication or EventType.LikedPublicationComment or
        EventType.NewBlogComment or EventType.LikedBlogComment or
        EventType.NewPublicationComment or
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
        EventType.StatusCharacterLeft or EventType.StatusCharacterReturned or
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
    /// options.
    /// </remarks>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="field">Name the generator declared the identifier by</param>
    private static string? Identifier(object? metadata, string field)
    {
        foreach (var (name, value) in NotificationText.ReadFields(metadata))
        {
            if (string.Equals(name, field, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }
}
