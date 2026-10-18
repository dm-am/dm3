using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// Ban type
/// </summary>
/// <remarks>
/// Derived from the stored ban, not sent with it: permanence is the length of the
/// window and voluntariness is a column. There is deliberately no Auto member. The
/// site does issue bans of its own on warning points, and one of them is a
/// <see cref="Temporary" /> ban and nothing else - a two-day window a senior
/// moderator then settles. Who issued it is already on the ban, in the moderator
/// field: the automatic one carries the system account there. A member of its own
/// would put the same fact in a second place, and the two would answer differently
/// the first time one of them was written by hand.
/// </remarks>
public enum BanType
{
    /// <summary>
    /// Temporary ban issued by moderator
    /// </summary>
    Temporary = 1,

    /// <summary>
    /// Permanent ban issued by moderator
    /// </summary>
    Permanent = 2,

    /// <summary>
    /// Voluntary ban requested by user
    /// </summary>
    Voluntary = 3
}

/// <summary>
/// Ban DTO
/// </summary>
public class Ban
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Banned user
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the ban
    /// </summary>
    public User? Moderator { get; set; }

    /// <summary>
    /// Ban type
    /// </summary>
    public BanType Type { get; set; }

    /// <summary>
    /// What the ban forbids: DemocraticBan, the ordinary ("демократический")
    /// one, or FullBan. Sent so that the list names the scope in words instead
    /// of guessing it from anything else.
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Ban start timestamp (UTC)
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end timestamp (UTC), null for permanent
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban reason, rendered: the window that issues a ban takes it in the BBCode
    /// editor, and it reads as a comment does
    /// </summary>
    public CommonBbText Comment { get; set; } = null!;

    /// <summary>
    /// Whether the ban is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the reader may lift the ban now. Answered by the rule that refuses
    /// the lift itself, so a list that offers the lift by this flag never offers
    /// one the server turns down: a senior moderator lifts a running ban of
    /// somebody else's, an administrator a permanent one as well.
    /// </summary>
    public bool CanLift { get; set; }

    /// <summary>
    /// Whether the ban was lifted early (distinguishes lifted from expired in history)
    /// </summary>
    public bool IsLifted { get; set; }

    /// <summary>
    /// Whether this is a voluntary ban requested by user
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// When the ban was lifted (if lifted early)
    /// </summary>
    public DateTimeOffset? LiftedUtc { get; set; }

    /// <summary>
    /// Moderator who lifted the ban early, null while the ban runs its course or
    /// when that account is gone
    /// </summary>
    /// <remarks>
    /// The user, as <see cref="Moderator" /> is: the ban list signs the lift with
    /// a link to whoever lifted it, as it signs the issue.
    /// </remarks>
    public User? LiftedBy { get; set; }

    /// <summary>
    /// Why the ban was lifted early
    /// </summary>
    public string? LiftReason { get; set; }
}

/// <summary>
/// What happened to a ban at a moment of the ban list
/// </summary>
public enum BanLogEntryKind
{
    /// <summary>
    /// The ban was issued; the entry stands at the start of the ban
    /// </summary>
    Issued = 1,

    /// <summary>
    /// The ban was lifted early; the entry stands at the moment of the lift
    /// </summary>
    Lifted = 2
}

/// <summary>
/// One entry of the ban list: an issue or a lift, at the moment it happened
/// </summary>
/// <remarks>
/// The list is written the way DM2's bot wrote its ban log, a comment for every
/// issue and every lift in the order they happened. A lifted ban therefore gives
/// two entries carrying the same ban: its issue, and its lift above every entry
/// that happened before the lift.
/// </remarks>
public class BanLogEntry
{
    /// <summary>
    /// Number of the entry in the whole list, counted from the oldest entry,
    /// which is 1, as DM2 numbered the comments of its ban log. A new entry
    /// happens after every entry there is, so the number of an entry never
    /// changes, and the list of one person shows the numbers of the whole list.
    /// The page of an entry is asked for by it (<see cref="BansQuery.Entry" />).
    /// </summary>
    /// <example>2077</example>
    public int Number { get; set; }

    /// <summary>
    /// Whether the entry is the issue of the ban or its lift
    /// </summary>
    public BanLogEntryKind Kind { get; set; }

    /// <summary>
    /// When it happened (UTC): the start of the ban for an issue, the moment of
    /// the lift for a lift
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// The ban as it stands now. For a lift the end date it carries is the end
    /// of the ban that was lifted, not the day of the lift.
    /// </summary>
    public Ban Ban { get; set; } = null!;
}

/// <summary>
/// Filter and page of the ban list
/// </summary>
public class BansQuery : PagingQuery
{
    /// <summary>
    /// Username the list is narrowed to. Empty is the whole website; a name nobody
    /// carries is an empty page rather than a refusal, as in the warning log.
    /// </summary>
    /// <example>problemuser</example>
    public string? Username { get; set; }

    /// <summary>
    /// Number of an entry (<see cref="BanLogEntry.Number" />) whose page of this
    /// list is asked for, in place of the page <c>skip</c> names: the link to an
    /// entry, which outlives the page number the entry stood on, since every new
    /// entry moves it down. An entry this list does not have, gone or of
    /// somebody else, leaves the page to <c>skip</c>. The page is told by
    /// <c>paging.skip</c> of the answer.
    /// </summary>
    /// <example>2077</example>
    [Range(1, int.MaxValue, ErrorMessage = "Номер записи начинается с 1")]
    public int? Entry { get; set; }
}

/// <summary>
/// Request to create a ban
/// </summary>
public class CreateBanRequest
{
    /// <summary>
    /// Target user username
    /// </summary>
    /// <example>problemuser</example>
    public string Username { get; set; } = "";

    /// <summary>
    /// Ban access restriction scope ("Тип бана" in the doc, 4.2.4.2):
    /// <see cref="AccessPolicy.DemocraticBan"/> silences public speech — the
    /// global chat, the forum, the discussion of other people's games and blogs —
    /// while the user's own games and blogs, posts in game rooms and direct
    /// messages stay open; <see cref="AccessPolicy.FullBan"/> blocks everything
    /// and fails authentication itself. This text is spelled out rather than
    /// linked because Swagger renders a cref as a bare type name, and a moderator
    /// picking the scope reads it here. Anything other than these two is refused
    /// with 400, an omitted scope included: a default here would answer a request
    /// that never named a scope with the strictest ban there is and report success.
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Ban expiration time (required for Temporary)
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban duration in hours (alternative to ExpiresUtc)
    /// </summary>
    public int? DurationHours { get; set; }

    /// <summary>
    /// Ban reason/comment
    /// </summary>
    /// <example>Repeated rule violations</example>
    public string Comment { get; set; } = "";
}

/// <summary>
/// Request to lift a ban early
/// </summary>
/// <remarks>
/// Sent in the body of the DELETE, the way the removal of an account sends its
/// reason: it is a record about a person, and addresses end up in logs.
/// </remarks>
public class LiftBanRequest
{
    /// <summary>
    /// Reason for lifting the ban early, required: the lift is drawn in the ban
    /// list as a decision of its own, and one without words would be the only
    /// decision there nobody explained
    /// </summary>
    /// <example>User appealed successfully</example>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Ban as visible to everyone on the public profile
/// </summary>
/// <remarks>
/// Facts only — ban reason and moderator identity stay in the
/// moderation area (GET /v1/bans, /v1/moderation/users endpoints).
/// </remarks>
public class PublicBan
{
    /// <summary>
    /// Ban type
    /// </summary>
    public BanType Type { get; set; }

    /// <summary>
    /// Ban start timestamp (UTC)
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end timestamp (UTC), null for permanent
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Whether the ban is currently active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// User ban status (public view)
/// </summary>
public class PublicUserBanStatus
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Whether user is currently banned
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// Active ban details (if banned), trimmed public view
    /// </summary>
    public PublicBan? ActiveBan { get; set; }

    /// <summary>
    /// Ban history (trimmed public view)
    /// </summary>
    public IEnumerable<PublicBan> History { get; set; } = Array.Empty<PublicBan>();
}

/// <summary>
/// User ban status (full view)
/// </summary>
/// <remarks>
/// Internal aggregation carrier for the moderation profile endpoints;
/// the public GET users/{username}/bans returns <see cref="PublicUserBanStatus"/>.
/// </remarks>
public class UserBanStatus
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Whether user is currently banned
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// Active ban details (if banned)
    /// </summary>
    public Ban? ActiveBan { get; set; }

    /// <summary>
    /// Ban history
    /// </summary>
    public IEnumerable<Ban> History { get; set; } = Array.Empty<Ban>();
}
