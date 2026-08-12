namespace DM.Domain.Moderation.Authorization;

/// <summary>
/// Moderation-related intentions
/// </summary>
public enum ModerationIntention
{
    // Bans, warnings, curatorship and the ban issued while resolving a ticket
    // compare the role inside their domain services, because the refusal there
    // has to name the role it wants (docs/architecture/AUTHORIZATION.md).
    // They get no intention here: an intention declared and resolved for an
    // action nobody routes through it is a second copy of the same threshold,
    // kept green by its own tests and free to drift away from the check that
    // actually runs.

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
