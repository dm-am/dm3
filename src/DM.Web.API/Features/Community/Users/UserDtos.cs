using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// Base user DTO for lists, author references, mentions, and cards
/// </summary>
public class User : UserRef
{
    /// <summary>
    /// History of username changes
    /// </summary>
    /// <remarks>
    /// Empty: the user never renamed. Absent: this response did not fetch the
    /// history, which only the user endpoints do.
    /// </remarks>
    public IReadOnlyCollection<UsernameHistoryEntry>? UsernameHistory { get; set; }

    /// <summary>
    /// User rating information
    /// </summary>
    /// <remarks>
    /// Null if user has disabled rating display (showRating=false).
    /// In UserProfile, rating is always present.
    /// </remarks>
    public Rating? Rating { get; set; }

    /// <summary>
    /// User profile picture (small URL only in lists)
    /// </summary>
    public UserPicture Picture { get; set; } = new();


    // The aggregates below are nullable and an absent one was not counted: nested in
    // someone else's resource, a user carries only what the query that fetched it
    // counted, so a zero would say "none" where nobody looked.

    /// <summary>
    /// Registration date (UTC)
    /// </summary>
    public DateTimeOffset? RegisteredUtc { get; set; }

    /// <summary>
    /// Number of games where user is master or assistant
    /// </summary>
    public int? GamesHosting { get; set; }

    /// <summary>
    /// Games hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesHostingByStatus { get; set; }

    /// <summary>
    /// Number of games where user is a player (has active character)
    /// </summary>
    public int? GamesPlaying { get; set; }

    /// <summary>
    /// Games playing breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesPlayingByStatus { get; set; }

    /// <summary>
    /// Number of blogs where user is owner or assistant
    /// </summary>
    public int? BlogsHosting { get; set; }

    /// <summary>
    /// Blogs hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? BlogsHostingByStatus { get; set; }

    /// <summary>
    /// Number of post reviews given to other users
    /// </summary>
    public int? ReviewsGiven { get; set; }

    /// <summary>
    /// Number of post reviews received from other users
    /// </summary>
    public int? ReviewsReceived { get; set; }

    /// <summary>
    /// Number of endorsements (user recommendations) written by this user
    /// about other users.
    /// </summary>
    public int? EndorsementsGiven { get; set; }

    /// <summary>
    /// Number of endorsements received by this user (other users wrote them).
    /// </summary>
    public int? EndorsementsReceived { get; set; }

    /// <summary>
    /// Number of game reviews written by this user. Reviews of whole games,
    /// not of single posts — <see cref="ReviewsGiven" /> is the post one.
    /// </summary>
    public int? GameReviewsGiven { get; set; }

    /// <summary>
    /// Number of game reviews received by this user: reviews written about the
    /// games they master.
    /// </summary>
    public int? GameReviewsReceived { get; set; }

    /// <summary>
    /// Forum topics authored by this user.
    /// </summary>
    public int? TopicsAuthored { get; set; }

    /// <summary>
    /// Comments authored by this user (polymorphic — across forum / blog /
    /// game / publication).
    /// </summary>
    public int? CommentsAuthored { get; set; }

    /// <summary>
    /// Messages this user has posted in the global chat.
    /// </summary>
    public int? GlobalChatMessages { get; set; }

    /// <summary>
    /// Number of bans this user has received. Drives the "резиновая уточка"
    /// achievement chain — an easter egg for the duckling-terrorists meme.
    /// </summary>
    public int? BansReceived { get; set; }

    /// <summary>
    /// Number of games this user has voluntarily dropped (Retired characters
    /// with IsPlayerLeft=true). Drives the "дропы" achievement chain.
    /// </summary>
    public int? GameDrops { get; set; }

    /// <summary>
    /// Number of publications (blog articles) authored by this user.
    /// Drives the "публикации" achievement chain.
    /// </summary>
    public int? PublicationsAuthored { get; set; }

    /// <summary>
    /// Total likes received on this user's authored content across
    /// topics, publications, comments and chat messages combined.
    /// Drives the "Нравится" achievement chain. Game posts excluded —
    /// they have their own quality signal via "Рейтинг".
    /// </summary>
    public int? LikesReceived { get; set; }

    /// <summary>
    /// How many subscribers each of the three profile categories has.
    /// </summary>
    public SubscriberCounts? SubscribersByCategory { get; set; }

    /// <summary>
    /// Subscriber refs for profile display: at most 20, most recently active
    /// first. A sample of the subscribers rather than the members of any one
    /// category — the cap is taken before the categories are considered, so
    /// <see cref="SubscribersByCategory" /> is what says how many there are.
    /// </summary>
    public IReadOnlyCollection<SubscriberRef>? Subscribers { get; set; }
}

/// <summary>
/// Subscriber totals per profile category.
/// </summary>
public class SubscriberCounts
{
    /// <summary>
    /// Subscribed to this user's games.
    /// </summary>
    public int Games { get; set; }

    /// <summary>
    /// Subscribed to this user's blogs.
    /// </summary>
    public int Blogs { get; set; }

    /// <summary>
    /// Subscribed to this user's topics.
    /// </summary>
    public int Topics { get; set; }
}

/// <summary>
/// Lightweight subscriber reference for profile-page display.
/// Just enough fields to render a styled router-link to the profile,
/// plus the subscription <see cref="Settings"/> bitmask so the profile
/// UI can filter the list per active tab ("subscribed to games / blogs
/// / topics") without a second round-trip.
/// </summary>
public class SubscriberRef
{
    /// <summary>
    /// Subscriber's display name.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Last activity moment (UTC). Null = never recorded.
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// Subscription settings bitmask (the <c>SubscriptionSettings</c>
    /// [Flags] enum — InApp + Email channels and the per-category author
    /// event bits). The profile UI ANDs this against the active tab's
    /// flag (AuthorGameEvents / AuthorBlogEvents / AuthorTopicEvents) to
    /// decide whether to render this subscriber in that tab's section.
    /// </summary>
    public int Settings { get; set; }
}

/// <summary>
/// Public user profile DTO for profile pages
/// </summary>
public class UserProfile : User
{
    /// <summary>
    /// User-defined status message
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// User-defined extended information (BB-code rendered)
    /// </summary>
    public InfoBbText? Info { get; set; }

    /// <summary>
    /// User gender
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// User birthday information
    /// </summary>
    /// <remarks>
    /// Null if user chose to hide birthday (showBirthday=false).
    /// </remarks>
    public Birthday? Birthday { get; set; }

    /// <summary>
    /// User real name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// User location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// User contact information
    /// </summary>
    public IReadOnlyCollection<Contact> Contacts { get; set; } = Array.Empty<Contact>();

    /// <summary>
    /// Caller's personal note about this user (null if none)
    /// </summary>
    /// <remarks>
    /// Only visible to the caller who created it.
    /// Different from ModeratorNotes which are shared among moderators.
    /// </remarks>
    public PersonalNote? PersonalNote { get; set; }

    /// <summary>
    /// Whether this user is on the viewer's personal blacklist. False for a guest
    /// and on the viewer's own profile.
    /// </summary>
    /// <remarks>
    /// Only the viewer's own list is read: whether this user blocked the viewer is
    /// theirs to know, and <see cref="Viewer" /> says no more than a refused message.
    /// </remarks>
    public bool BlockedByViewer { get; set; }

    /// <summary>
    /// What the viewer may do with this user. Null on the profile of "Аккаунт
    /// удален" and in the answers of the moderation: it is answered on the read
    /// of a profile, the shape the page offers its actions from
    /// </summary>
    public ProfileViewer? Viewer { get; set; }

    /// <summary>
    /// User profile picture (small + medium URLs in profile view)
    /// </summary>
    public new UserPicture Picture { get; set; } = new();
}

/// <summary>
/// What the viewer may do with the user of a profile. Each flag is asked by the
/// same code the write it stands for is refused by, so a page that offers by it
/// offers only what the server takes; the server refuses all the same.
/// </summary>
public class ProfileViewer
{
    /// <summary>
    /// Whether the viewer may write to this user in private: signed in, not his
    /// own profile, and not on this user's blacklist with private messages
    /// blocked. The last is what the refusal of the message would have said
    /// after it was written, and says nothing more
    /// </summary>
    public bool CanMessage { get; set; }
}

/// <summary>
/// Rating information
/// </summary>
public class Rating
{
    /// <summary>
    /// Total posts count (quantity rating)
    /// </summary>
    public int TotalPosts { get; set; }

    /// <summary>
    /// Sum of post review scores received (quality rating)
    /// </summary>
    public int PostReviewScoreSum { get; set; }
}

/// <summary>
/// DTO model for contact information
/// </summary>
public class Contact
{
    /// <summary>
    /// Contact type (e.g., "Telegram", "Discord", "Email")
    /// </summary>
    public string ContactType { get; set; } = string.Empty;

    /// <summary>
    /// Contact value (e.g., username, email address)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO model for birthday
/// </summary>
public class Birthday
{
    /// <summary>
    /// Day of birth (1-31)
    /// </summary>
    public int Day { get; set; }

    /// <summary>
    /// Month of birth (1-12)
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Year of birth
    /// </summary>
    public int Year { get; set; }
}

/// <summary>
/// DTO model for user profile picture
/// </summary>
public class UserPicture
{
    /// <summary>
    /// Picture identifier (for deletion, only in PersonalProfile context)
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Small picture URL (used in lists)
    /// </summary>
    public string? SmallUrl { get; set; }

    /// <summary>
    /// Medium picture URL (used in profiles)
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Original picture URL (full size, only in PersonalProfile context)
    /// </summary>
    public string? OriginalUrl { get; set; }

    /// <summary>
    /// Intrinsic width of <see cref="OriginalUrl"/> in pixels.
    /// </summary>
    /// <remarks>
    /// Only the original keeps the uploaded aspect ratio; null for an upload older
    /// than the measurement.
    /// </remarks>
    public int? OriginalWidth { get; set; }

    /// <summary>
    /// Intrinsic height of <see cref="OriginalUrl"/> in pixels. Sent together
    /// with <see cref="OriginalWidth"/> — one without the other says nothing
    /// about the ratio.
    /// </summary>
    public int? OriginalHeight { get; set; }
}

/// <summary>
/// Entry in username change history
/// </summary>
public class UsernameHistoryEntry
{
    /// <summary>
    /// Previous username before change
    /// </summary>
    public string OldUsername { get; set; } = string.Empty;

    /// <summary>
    /// When the username was changed (UTC)
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }
}

/// <summary>
/// Query parameters for user list filtering
/// </summary>
public class UsersQuery : PagingQuery
{
    /// <summary>
    /// Search by username prefix
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by user role
    /// </summary>
    public UserRole? Role { get; set; }

    /// <summary>
    /// Filter by activity status
    /// </summary>
    /// <remarks>
    /// Active - online recently, Inactive - not, All - all users.
    /// </remarks>
    public UserActivityFilter Activity { get; set; } = UserActivityFilter.Active;

    /// <summary>
    /// Filter to show only currently online users
    /// </summary>
    /// <remarks>
    /// When true, only shows users who are currently online (activity within last 5 minutes).
    /// Requires Activity=Active to be meaningful.
    /// </remarks>
    public bool? IsOnline { get; set; }

    /// <summary>
    /// Filter by newbie status (users with less than 100 posts)
    /// </summary>
    /// <remarks>
    /// true = only newbies, false = only experienced, null = all
    /// </remarks>
    public bool? IsNewbie { get; set; }

    /// <summary>
    /// Minimum rating (post review score sum) filter
    /// </summary>
    public int? MinRating { get; set; }

    /// <summary>
    /// Maximum rating (post review score sum) filter
    /// </summary>
    public int? MaxRating { get; set; }

    /// <summary>
    /// Minimum number of games hosting (master or assistant)
    /// </summary>
    public int? MinGamesHosting { get; set; }

    /// <summary>
    /// Maximum number of games hosting (master or assistant)
    /// </summary>
    public int? MaxGamesHosting { get; set; }

    /// <summary>
    /// Minimum number of games playing (has active character)
    /// </summary>
    public int? MinGamesPlaying { get; set; }

    /// <summary>
    /// Maximum number of games playing (has active character)
    /// </summary>
    public int? MaxGamesPlaying { get; set; }

    /// <summary>
    /// Minimum number of blogs hosting (owner or assistant)
    /// </summary>
    public int? MinBlogsHosting { get; set; }

    /// <summary>
    /// Maximum number of blogs hosting (owner or assistant)
    /// </summary>
    public int? MaxBlogsHosting { get; set; }

    /// <summary>
    /// Filter by registration date - from (inclusive)
    /// </summary>
    public DateTimeOffset? RegisteredFromUtc { get; set; }

    /// <summary>
    /// Filter by registration date - to (inclusive)
    /// </summary>
    public DateTimeOffset? RegisteredToUtc { get; set; }

    /// <summary>
    /// Sort field
    /// </summary>
    public UserSort SortBy { get; set; } = UserSort.Name;

    /// <summary>
    /// Sort direction: asc or desc
    /// </summary>
    public string? SortOrder { get; set; }
}

/// <summary>
/// Caller's personal note about another user
/// </summary>
/// <remarks>
/// Private note visible only to the author.
/// Created via POST /v1/personal/profile-notes.
/// </remarks>
public class PersonalNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }
}

/// <summary>
/// Login history entry
/// </summary>
public class LoginHistoryDto
{
    /// <summary>
    /// Login timestamp
    /// </summary>
    public DateTimeOffset LoginTimestampUtc { get; set; }

    /// <summary>
    /// IP address used for login
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Count breakdown by module status (for games/blogs)
/// </summary>
public class ModuleStatusCounts
{
    /// <summary>
    /// Number of items in Draft status
    /// </summary>
    public int Draft { get; set; }

    /// <summary>
    /// Number of items in Active status
    /// </summary>
    public int Active { get; set; }

    /// <summary>
    /// Number of items in Closed status
    /// </summary>
    public int Closed { get; set; }
}
