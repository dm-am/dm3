using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// Aggregated moderation profile for a user.
/// Fields are filtered by caller's role:
/// Admin sees all fields; Moderator sees linked profiles, notes, violations.
/// </summary>
/// <remarks>
/// Inherits from UserProfile: Id, Username, Role, Rating, Picture,
/// Status, Info, Gender, Birthday, Name, Location, Contacts, RegisteredAtUtc, etc.
/// Adds moderation-specific fields: Email (admin only), IpAddresses, LoginHistory,
/// LinkedProfiles, ModeratorNotes, Violations, Permissions.
/// </remarks>
public class ModeratedProfile : UserProfile
{
    /// <summary>
    /// User email. Null if caller is not Admin.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// IP addresses used for login (last 365 days, unique, with stats).
    /// Null if caller is not Admin.
    /// </summary>
    public IReadOnlyList<UserIpInfo>? IpAddresses { get; set; }

    /// <summary>
    /// Recent login history (last 50 entries).
    /// Null if caller is not Admin.
    /// </summary>
    public IReadOnlyList<LoginRecord>? LoginHistory { get; set; }

    /// <summary>
    /// Users sharing IPs with this user (successful logins only).
    /// </summary>
    public IReadOnlyList<LinkedProfile> LinkedProfiles { get; set; } = [];

    /// <summary>
    /// Moderator notes about this user.
    /// </summary>
    public IReadOnlyList<ModNote> ModeratorNotes { get; set; } = [];

    /// <summary>
    /// Summary of violations (warning/ban counts and active status).
    /// </summary>
    public ViolationSummary Violations { get; set; } = new();

    /// <summary>
    /// Whether moderation is watching what this user creates: while it is on,
    /// every game and blog they create starts in premoderation.
    /// </summary>
    public bool IsUnderModerationWatch { get; set; }

    /// <summary>
    /// What actions the current user can perform.
    /// Computed based on caller's role.
    /// </summary>
    public ModerationPermissions Permissions { get; set; } = new();
}

/// <summary>
/// Aggregated IP info: IP address with first/last seen timestamps and login count
/// </summary>
public class UserIpInfo
{
    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// First login from this IP (UTC)
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; set; }

    /// <summary>
    /// Most recent login from this IP (UTC)
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; set; }

    /// <summary>
    /// Total number of logins from this IP
    /// </summary>
    public int LoginsCount { get; set; }
}

/// <summary>
/// Individual login record (for login history)
/// </summary>
public class LoginRecord
{
    /// <summary>
    /// Login timestamp (UTC)
    /// </summary>
    public DateTimeOffset LoginUtc { get; set; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Browser User-Agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Whether the login was successful
    /// </summary>
    public bool IsSuccessful { get; set; }
}

/// <summary>
/// User sharing IP addresses with the target user
/// </summary>
public class LinkedProfile
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Number of shared IP addresses
    /// </summary>
    public int SharedIpsCount { get; set; }

    /// <summary>
    /// Most recent shared login timestamp (UTC)
    /// </summary>
    public DateTimeOffset LastSharedLoginUtc { get; set; }
}

/// <summary>
/// Moderator note about a user (within aggregated moderation profile)
/// </summary>
public class ModNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username of the moderator who created the note
    /// </summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the moderator who created the note
    /// </summary>
    public Guid AuthorId { get; set; }

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

    /// <summary>
    /// Whether the current caller can edit this note
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Whether the current caller can delete this note
    /// </summary>
    public bool CanDelete { get; set; }
}

/// <summary>
/// Summary of a user's violations (warnings and bans)
/// </summary>
public class ViolationSummary
{
    /// <summary>
    /// Total number of warnings ever issued
    /// </summary>
    public int TotalWarnings { get; set; }

    /// <summary>
    /// Sum of active warning points (within last 30 days)
    /// </summary>
    public int ActiveWarningPoints { get; set; }

    /// <summary>
    /// Total number of bans ever issued
    /// </summary>
    public int TotalBans { get; set; }

    /// <summary>
    /// Whether the user is currently banned
    /// </summary>
    public bool IsCurrentlyBanned { get; set; }

    /// <summary>
    /// End date of current ban (null if permanent or not banned)
    /// </summary>
    public DateTimeOffset? CurrentBanEndUtc { get; set; }

    /// <summary>
    /// Reason for current ban (null if not banned)
    /// </summary>
    public string? CurrentBanReason { get; set; }
}

/// <summary>
/// What actions the current moderator can perform on this profile.
/// Computed server-side based on caller role.
/// </summary>
public class ModerationPermissions
{
    /// <summary>
    /// Can view user email (Admin only)
    /// </summary>
    public bool CanViewEmail { get; set; }

    /// <summary>
    /// Can view IP addresses and login history (Admin only)
    /// </summary>
    public bool CanViewIpAddresses { get; set; }

    /// <summary>
    /// Can view login history (Admin only)
    /// </summary>
    public bool CanViewLoginHistory { get; set; }

    /// <summary>
    /// Can view linked profiles (Moderator+)
    /// </summary>
    public bool CanViewLinkedProfiles { get; set; }

    /// <summary>
    /// Can view moderator notes (Moderator+)
    /// </summary>
    public bool CanViewModNotes { get; set; }

    /// <summary>
    /// Can create moderator notes (Moderator+)
    /// </summary>
    public bool CanCreateModNote { get; set; }

    /// <summary>
    /// Can issue warnings: Moderator+, to a user whose role is below the caller's
    /// and who is not an administrator (ModerationRank)
    /// </summary>
    public bool CanIssueWarning { get; set; }

    /// <summary>
    /// Can issue bans: SeniorModerator+, to a user whose role is below the
    /// caller's and who is not an administrator (ModerationRank)
    /// </summary>
    public bool CanIssueBan { get; set; }

    /// <summary>
    /// Can lift bans (SeniorModerator+)
    /// </summary>
    public bool CanLiftBan { get; set; }

    /// <summary>
    /// Can put the user under moderation watch and take them off it (Moderator+)
    /// </summary>
    public bool CanSetModerationWatch { get; set; }
}

/// <summary>
/// DTO for moderating user profile (SeniorModerator+ only)
/// </summary>
public class ModerateProfile
{
    /// <summary>
    /// User-defined extended information (can be cleared by moderator)
    /// </summary>
    public string? Info { get; set; }
}

/// <summary>
/// DTO for switching the moderation watch on a user (Moderator+ only)
/// </summary>
public class ModerationWatchRequest
{
    /// <summary>
    /// Whether the user is under moderation watch from now on
    /// </summary>
    public bool UnderWatch { get; set; }
}

/// <summary>
/// Request to remove a user's account on a moderator's decision
/// </summary>
/// <remarks>
/// The reason is required. An account removed with nothing written down leaves
/// the next moderator - and the reader who asks what happened - with a missing
/// person and no record of who took the decision or why.
/// </remarks>
public class RemoveAccountRequest
{
    /// <summary>
    /// Why the account is being removed
    /// </summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// An account moderation has removed, as the one list that can see one carries it
/// </summary>
/// <remarks>
/// Not one of the user references the rest of the API answers with: those are
/// drawn as a card, with a rating, a picture and a newbie mark, and none of that
/// exists for a row every other read in the product hides. What is left is what a
/// moderator holding a support letter recognises the account by.
/// </remarks>
public class RemovedAccount
{
    /// <summary>
    /// Account identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name the account keeps while it is gone
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Address the account keeps. Absent unless the caller is an administrator.
    /// </summary>
    /// <remarks>
    /// Withheld below an administrator by the domain, and not a second time here:
    /// the rule is the same one the moderated profile obeys, and a copy of it in
    /// this layer would be a second answer to the question of who may read an
    /// address.
    /// </remarks>
    public string? Email { get; set; }

    /// <summary>
    /// Role the account had
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Registration moment (UTC)
    /// </summary>
    public DateTimeOffset RegisteredUtc { get; set; }

    /// <summary>
    /// Last activity moment (UTC), if the account was ever active
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }
}

/// <summary>
/// Which removed accounts to list
/// </summary>
/// <remarks>
/// One term for both columns: a moderator answering a support letter has a name
/// or an address and nothing else, and whoever wrote the letter is the one who
/// decided which of the two he quoted.
/// </remarks>
public class RemovedAccountsQuery : PagingQuery
{
    /// <summary>
    /// Part of a name or of an address; empty lists every removed account
    /// </summary>
    /// <example>ivan</example>
    public string? Search { get; set; }
}

/// <summary>
/// Moderator note about a user
/// </summary>
public class ModeratedProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User this note is about
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who created the note
    /// </summary>
    public User Author { get; set; } = null!;

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
/// Request to create a moderator note
/// </summary>
public class CreateModeratedProfileNoteRequest
{
    /// <summary>
    /// Note text
    /// </summary>
    /// <example>User has been warned about spamming in the past</example>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a moderator note
/// </summary>
public class UpdateModeratedProfileNoteRequest
{
    /// <summary>
    /// Updated note text
    /// </summary>
    /// <example>Updated note: User has improved their behavior</example>
    public string Text { get; set; } = string.Empty;
}

