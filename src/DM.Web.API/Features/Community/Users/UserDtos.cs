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
/// <remarks>
/// This is the minimal user representation used in:
/// - GET /v1/users (list)
/// - GET /v1/users/{username}
/// - post.author, comment.author, etc.
///
/// For full profile information, use UserProfile or PersonalProfile.
/// </remarks>
public class User
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// History of username changes
    /// </summary>
    /// <remarks>
    /// Always included. Most users have empty array [].
    /// Helps identify "is this the former johnny?"
    /// </remarks>
    public IReadOnlyCollection<UsernameHistoryEntry> UsernameHistory { get; set; } = Array.Empty<UsernameHistoryEntry>();

    /// <summary>
    /// User role (RegularUser, Mentor, Moderator, etc.)
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Honorary status (visual badge for former moderators, helpers, etc.)
    /// </summary>
    /// <remarks>
    /// Visual distinction only, no additional permissions.
    /// </remarks>
    public bool IsHonorary { get; set; }

    /// <summary>
    /// Newbie status (less than 100 posts)
    /// </summary>
    /// <remarks>
    /// Computed from QuantityRating in database.
    /// Used in UI and business logic (restrictions on reviews).
    /// </remarks>
    public bool IsNewbie { get; set; }

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

    /// <summary>
    /// Last activity moment (UTC)
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }
}

/// <summary>
/// Public user profile DTO for profile pages
/// </summary>
/// <remarks>
/// Used for: GET /v1/users/{username}/profile
/// Extends User with additional profile information.
///
/// Privacy rules:
/// - birthday is null if visibility.showBirthday=false
/// - rating is always present (even if hidden in User lists)
/// </remarks>
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
    /// User registration date (UTC)
    /// </summary>
    public DateTimeOffset RegisteredAtUtc { get; set; }

    /// <summary>
    /// User's featured post (highest rated)
    /// </summary>
    /// <remarks>
    /// Automatically selected post with highest rating.
    /// Null if user has no posts.
    /// AuthorUsername is null (implied by profile context).
    /// </remarks>
    public FeaturedPost? FeaturedPost { get; set; }

    /// <summary>
    /// Number of post reviews given to other users
    /// </summary>
    public int PostReviewsGiven { get; set; }

    /// <summary>
    /// Number of post reviews received from other users
    /// </summary>
    public int PostReviewsReceived { get; set; }

    /// <summary>
    /// Caller's personal note about this user (null if none)
    /// </summary>
    /// <remarks>
    /// Only visible to the caller who created it.
    /// Different from ModeratorNotes which are shared among moderators.
    /// </remarks>
    public PersonalNote? PersonalNote { get; set; }

    /// <summary>
    /// User profile picture (small + medium URLs in profile view)
    /// </summary>
    /// <remarks>
    /// Uses 'new' to hide base User.Picture for different mapping:
    /// - User: SmallUrl only (for lists)
    /// - UserProfile: SmallUrl + MediumUrl (for profile page)
    /// - PersonalProfile uses AccountPicture which includes OriginalUrl
    /// </remarks>
    public new UserPicture Picture { get; set; } = new();
}

/// <summary>
/// Rating information
/// </summary>
/// <remarks>
/// Rating can be null in User DTO if user has disabled rating display (showRating=false).
/// In UserProfile, rating is always present regardless of this setting.
/// </remarks>
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
    /// Small picture URL (100x100, used in lists)
    /// </summary>
    public string? SmallUrl { get; set; }

    /// <summary>
    /// Medium picture URL (200x200, used in profiles)
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Original picture URL (full size, only in PersonalProfile context)
    /// </summary>
    public string? OriginalUrl { get; set; }
}

/// <summary>
/// Entry in username change history
/// </summary>
/// <remarks>
/// Included in User DTO to help identify users who changed their names.
/// Most users will have an empty history array.
/// </remarks>
public class UsernameHistoryEntry
{
    /// <summary>
    /// Previous username before change
    /// </summary>
    public string OldUsername { get; set; } = string.Empty;

    /// <summary>
    /// When the username was changed (UTC)
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; }
}

/// <summary>
/// Query parameters for user list filtering
/// </summary>
/// <remarks>
/// GET /v1/users?q=john&amp;role=Mentor&amp;activity=Active&amp;skip=0&amp;take=20&amp;sort=name
/// </remarks>
public class UsersQuery : PagingQuery
{
    /// <summary>
    /// Search by username prefix
    /// </summary>
    public string? Q { get; set; }

    /// <summary>
    /// Filter by user role
    /// </summary>
    public UserRole? Role { get; set; }

    /// <summary>
    /// Filter by activity status
    /// </summary>
    /// <remarks>
    /// Active - online recently, All - all users, Pending - for moderators only.
    /// </remarks>
    public UserActivityFilter Activity { get; set; } = UserActivityFilter.Active;

    /// <summary>
    /// Sort order
    /// </summary>
    /// <remarks>
    /// Name - alphabetically (default), Rating - by post review score sum descending.
    /// </remarks>
    public UserSort Sort { get; set; } = UserSort.Name;
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
    public DateTimeOffset? UpdatedUtc { get; set; }
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
