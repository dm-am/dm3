using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Reads the object a warning was issued for: its text at issue time, and
/// where it stands now.
/// </summary>
/// <remarks>
/// A warning names the offending object by identifier alone, and the author can
/// edit that object afterwards. The snapshot taken here is what makes the
/// warning readable later, and it is the text the log shows: what the object
/// says now, or whether it was edited since, is not the warning's business.
/// The address is derived on every read, because it changes on its own and a
/// stored copy would be stale by the time a moderator looks.
///
/// Game posts and game room chat are deliberately absent: game content is not
/// moderated, so no warning names it and nothing here resolves it.
/// </remarks>
public interface IWarningEntityResolver
{
    /// <summary>
    /// Read the current text of the offending object, to be stored on the
    /// warning. Returns null when there is no such object, or when its kind
    /// carries no text moderation reads.
    /// </summary>
    Task<string?> CaptureSnapshot(WarningEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Read what the profile of the user shows everybody, to be stored on a
    /// warning issued on that profile: the status, "О себе" and the upload the
    /// avatar is. Returns null when there is no such user.
    /// </summary>
    /// <remarks>
    /// The one read of a user row here, and a plain one under the filter of
    /// removed accounts: the warned user was just found by his name.
    /// </remarks>
    Task<ProfileCapture?> CaptureProfile(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resolve the current state of the objects named by the given warnings,
    /// keyed by warning identifier. Warnings naming no object are absent from
    /// the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, WarningEntityState>> ResolveStates(
        IReadOnlyCollection<WarningEntityRequest> requests, CancellationToken ct = default);
}

/// <summary>
/// One warning's reference to the object it was issued for
/// </summary>
public class WarningEntityRequest
{
    /// <summary>
    /// Warning the reference belongs to
    /// </summary>
    public Guid WarningId { get; set; }

    /// <summary>
    /// Offending object identifier
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Offending object kind
    /// </summary>
    public WarningEntityType EntityType { get; set; }
}

/// <summary>
/// What is currently true about the object a warning was issued for
/// </summary>
public class WarningEntityState
{
    /// <summary>
    /// Site-relative address of the object, null when it cannot be addressed
    /// (deleted, or a kind with no public page)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Where the object stands, null when nothing says so
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of the place: the topic, the game, the blog or the publication.
    /// Null for the global chat, which the reader names by its kind, and for an
    /// unknown place.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// When the object itself was written, null when it was not found
    /// </summary>
    public DateTimeOffset? CreatedUtc { get; set; }

    /// <summary>
    /// Why a guest could not follow the link to the object, null when he could.
    /// Read now and not at issue time: a place that closes later takes the link
    /// out of the log too.
    /// </summary>
    public WarningLogClosure? LogClosure { get; set; }
}

/// <summary>
/// The place the offending text stood in, as the caption of a warning names it
/// </summary>
public enum WarningPlaceKind
{
    /// <summary>
    /// A forum topic: the topic itself or a comment under it
    /// </summary>
    Topic = 1,

    /// <summary>
    /// The discussion of a game
    /// </summary>
    Game = 2,

    /// <summary>
    /// The discussion of a blog
    /// </summary>
    Blog = 3,

    /// <summary>
    /// The global chat
    /// </summary>
    GlobalChat = 4,

    /// <summary>
    /// A publication of a blog: a comment under it
    /// </summary>
    Publication = 5
}

/// <summary>
/// Why a guest could not follow the link to the text a warning was issued for
/// </summary>
/// <remarks>
/// The open log shows the text and its place always, the owner's word for it
/// (05.10: "Всегда показывай"): a warning is read for what it was given for,
/// and a text its author or moderation took off the site afterwards is still
/// what it was given for. Hidden blocks are erased from it for everybody, as on
/// every surface that does not declare them. What a closure takes away is the
/// link alone: it would lead a guest to a page he cannot read, or to nothing.
/// </remarks>
public enum WarningLogClosure
{
    /// <summary>
    /// The object is not found, or its place is not one a warning addresses
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Deleted by somebody other than its author: moderation took it off the site
    /// </summary>
    RemovedByModeration = 2,

    /// <summary>
    /// The topic, the game, the blog or the publication it stood in is deleted
    /// </summary>
    PlaceRemoved = 3,

    /// <summary>
    /// The game or the blog is not visible to a guest (a draft without the open
    /// preview, or premoderation), or the publication is not published
    /// </summary>
    PlaceNotPublic = 4,

    /// <summary>
    /// The forum section is closed to guests
    /// </summary>
    BoardClosed = 6
}
