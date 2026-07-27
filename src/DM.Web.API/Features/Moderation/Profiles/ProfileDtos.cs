using System;
using System.Collections.Generic;
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
    /// Can issue warnings (Moderator+)
    /// </summary>
    public bool CanIssueWarning { get; set; }

    /// <summary>
    /// Can issue bans (SeniorModerator+)
    /// </summary>
    public bool CanIssueBan { get; set; }

    /// <summary>
    /// Can lift bans (SeniorModerator+)
    /// </summary>
    public bool CanLiftBan { get; set; }
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

/// <summary>
/// Aggregated moderation profile for a user (DTO version).
/// Fields are filtered by caller's role:
/// Admin sees all fields; Moderator sees linked profiles, notes, violations.
/// </summary>
public class ModerationProfileDto
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User email. Null if caller is not Admin.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User registration date (UTC)
    /// </summary>
    public DateTimeOffset RegistrationUtc { get; set; }

    /// <summary>
    /// IP addresses used for login (last 365 days, unique, with stats).
    /// Null if caller is not Admin.
    /// </summary>
    public IReadOnlyList<UserIpInfoDto>? IpAddresses { get; set; }

    /// <summary>
    /// Recent login history (last 50 entries).
    /// Null if caller is not Admin.
    /// </summary>
    public IReadOnlyList<LoginRecordDto>? LoginHistory { get; set; }

    /// <summary>
    /// Users sharing IPs with this user (successful logins only).
    /// </summary>
    public IReadOnlyList<LinkedProfileDto> LinkedProfiles { get; set; } = [];

    /// <summary>
    /// Moderator notes about this user.
    /// </summary>
    public IReadOnlyList<ModNoteDto> ModeratorNotes { get; set; } = [];

    /// <summary>
    /// Summary of violations (warning/ban counts and active status).
    /// </summary>
    public ViolationSummaryDto Violations { get; set; } = new();

    /// <summary>
    /// What actions the current user can perform.
    /// Computed based on caller's role.
    /// </summary>
    public ModerationPermissionsDto Permissions { get; set; } = new();
}

/// <summary>
/// Aggregated IP info: IP address with first/last seen timestamps and login count
/// </summary>
public class UserIpInfoDto
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
public class LoginRecordDto
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
public class LinkedProfileDto
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User username
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
public class ModNoteDto
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
public class ViolationSummaryDto
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
public class ModerationPermissionsDto
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
    /// Can issue warnings (Moderator+)
    /// </summary>
    public bool CanIssueWarning { get; set; }

    /// <summary>
    /// Can issue bans (SeniorModerator+)
    /// </summary>
    public bool CanIssueBan { get; set; }

    /// <summary>
    /// Can lift bans (SeniorModerator+)
    /// </summary>
    public bool CanLiftBan { get; set; }
}
