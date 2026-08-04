namespace DM.Domain.Core.Enums;

/// <summary>
/// Category of a moderation ticket ("обращение").
/// Visibility of each category on the moderation list endpoints is derived
/// from the caller role: Moderator sees user complaints and suggestions,
/// SeniorModerator additionally sees complaints about moderator decisions,
/// Admin sees everything including support categories.
/// </summary>
public enum TicketSubtype
{
    /// <summary>
    /// Complaint about a user (visible to moderators and above)
    /// </summary>
    UserComplaint = 0,

    /// <summary>
    /// Complaint about a junior moderator decision (visible to senior moderators and above)
    /// </summary>
    ModeratorDecisionComplaint = 1,

    /// <summary>
    /// Complaint about a senior moderator decision (visible to administrators)
    /// </summary>
    SeniorModeratorDecisionComplaint = 2,

    /// <summary>
    /// Site improvement suggestion (visible to moderators and above)
    /// </summary>
    SiteImprovementSuggestion = 3,

    /// <summary>
    /// Bug report (visible to administrators)
    /// </summary>
    Bug = 4,

    /// <summary>
    /// Account access recovery request (visible to administrators)
    /// </summary>
    AccessRecovery = 5,

    /// <summary>
    /// Registration problems (visible to administrators)
    /// </summary>
    RegistrationIssue = 6
}
