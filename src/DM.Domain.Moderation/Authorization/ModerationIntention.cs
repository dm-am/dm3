namespace DM.Domain.Moderation.Authorization;

/// <summary>
/// Moderation-related intentions
/// </summary>
public enum ModerationIntention
{
    /// <summary>
    /// View all active bans
    /// </summary>
    ViewAllBans,

    /// <summary>
    /// Create a ban for a user
    /// </summary>
    CreateBan,

    /// <summary>
    /// Lift an existing ban
    /// </summary>
    LiftBan,

    /// <summary>
    /// Create a warning for a user
    /// </summary>
    CreateWarning,

    /// <summary>
    /// Remove a warning
    /// </summary>
    RemoveWarning,

    /// <summary>
    /// View moderator notes about users
    /// </summary>
    ViewModNotes,

    /// <summary>
    /// Create moderator notes
    /// </summary>
    CreateModNote,

    /// <summary>
    /// Edit moderator notes
    /// </summary>
    EditModNote,

    /// <summary>
    /// Delete moderator notes
    /// </summary>
    DeleteModNote,

    /// <summary>
    /// View all moderation tickets
    /// </summary>
    ViewTickets,

    /// <summary>
    /// Create a moderation ticket
    /// </summary>
    CreateTicket,

    /// <summary>
    /// Resolve a moderation ticket
    /// </summary>
    ResolveTicket,

    /// <summary>
    /// View user's linked profiles
    /// </summary>
    ViewLinkedProfiles,

    /// <summary>
    /// Manage user credentials (force password reset, etc.)
    /// </summary>
    ManageCredentials,

    /// <summary>
    /// Set user role (Admin only)
    /// </summary>
    SetUserRole,

    /// <summary>
    /// Moderate user profile (edit Info field)
    /// </summary>
    ModerateUserProfile,

    /// <summary>
    /// Manage tags (create, edit, delete)
    /// </summary>
    ManageTags
}
