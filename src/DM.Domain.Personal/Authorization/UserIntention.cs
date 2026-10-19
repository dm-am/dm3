namespace DM.Domain.Personal.Authorization;

/// <summary>
/// List of user actions that require authorization
/// </summary>
public enum UserIntention
{
    /// <summary>
    /// Edit own user details
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Participate in dialogues with user
    /// </summary>
    WriteMessage = 2,

    /// <summary>
    /// Moderate user profile
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// Read moderator notes about users
    /// </summary>
    ReadModNotes = 4,

    /// <summary>
    /// Create moderator notes about users
    /// </summary>
    CreateModNote = 5,

    /// <summary>
    /// View the aggregated moderated profile block (linked profiles, notes, violations)
    /// </summary>
    ViewModeratedProfile = 8,

    /// <summary>
    /// View IP addresses and login history of a user
    /// </summary>
    ViewUserIpData = 9
}
