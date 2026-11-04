namespace DM.Domain.Core.Enums;

/// <summary>
/// One of the roles a user may hold in a game.
///
/// A user holds a set of these at once and resolvers ask it with Contains:
/// the numbering is no privilege ladder, do not compare by it.
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
