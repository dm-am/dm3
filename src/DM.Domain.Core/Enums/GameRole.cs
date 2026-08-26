namespace DM.Domain.Core.Enums;

/// <summary>
/// One of the roles a user may hold in a game.
///
/// Not mutually exclusive, and not a privilege ladder: a user holds a SET of
/// these at once - GameRoleExtensions.GetRoles returns a collection and every
/// resolver asks it with Contains, never with a comparison. The master of a
/// game is also a player in it whenever a character of theirs was accepted.
/// The numbering orders the declarations and nothing else; do not compare by it.
///
/// Applicant is declared and never assigned: an application in review puts
/// nobody into any role. It stays because the value names a filter of the
/// users endpoint, and renumbering the rest to remove it would buy nothing.
/// </summary>
public enum GameRole
{
    /// <summary>
    /// User is not participating in the game
    /// </summary>
    None = 0,

    /// <summary>
    /// Reader - subscribed to game updates (from Subscriptions table)
    /// </summary>
    Reader = 1,

    /// <summary>
    /// Applicant - has a pending character application
    /// </summary>
    Applicant = 2,

    /// <summary>
    /// Player - has an active (accepted) character
    /// </summary>
    Player = 3,

    /// <summary>
    /// Mentor - game curator (from Game.MentorId)
    /// </summary>
    Mentor = 4,

    /// <summary>
    /// Assistant - helps the master (from GameAssistants table)
    /// </summary>
    Assistant = 5,

    /// <summary>
    /// Master - game creator and owner (from Game.MasterId)
    /// </summary>
    Master = 6
}
