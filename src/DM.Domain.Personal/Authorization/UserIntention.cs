namespace DM.Domain.Personal.Authorization;

/// <summary>
/// List of user actions that require authorization
/// </summary>
public enum UserIntention
{
    /// <summary>
    /// Edit own user details (owner only)
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Participate in dialogues with user
    /// </summary>
    WriteMessage = 2,

    /// <summary>
    /// Moderate user profile (SeniorModerator+ can edit Info field)
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// Read moderator notes about users (Moderator+)
    /// </summary>
    ReadModNotes = 4,

    /// <summary>
    /// Create moderator notes about users (Moderator+)
    /// </summary>
    CreateModNote = 5,

    /// <summary>
    /// Edit or delete moderator notes (SeniorModerator+ for others' notes, own notes for all moderators)
    /// </summary>
    EditModNote = 6,

    /// <summary>
    /// Delete moderator notes (SeniorModerator+ for others' notes, own notes for all moderators)
    /// </summary>
    DeleteModNote = 7,

    /// <summary>
    /// View the aggregated moderated profile block (linked profiles, notes, violations).
    /// Requires: Moderator+
    /// </summary>
    ViewModeratedProfile = 8,

    /// <summary>
    /// View IP addresses and login history of a user.
    /// Requires: Admin
    /// </summary>
    ViewUserIpData = 9,

    /// <summary>
    /// View list of users pending activation.
    /// Requires: SeniorModerator+
    /// </summary>
    ViewPendingUsers = 10
}
