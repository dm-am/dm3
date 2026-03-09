namespace DM.Domain.Core.Enums;

/// <summary>
/// User's role in a game (mutually exclusive, ordered by privilege level)
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
    /// Master - game creator and owner (from Game.AuthorId)
    /// </summary>
    Master = 6
}
