namespace DM.Services.Gaming.Authorization;

/// <summary>
/// List of game actions that requires authorization
/// </summary>
public enum GameIntention
{
    /// <summary>
    /// Create new game
    /// </summary>
    Create = 0,

    /// <summary>
    /// Read game details
    /// </summary>
    Read = 1,

    /// <summary>
    /// Edit game
    /// </summary>
    Edit = 2,

    /// <summary>
    /// Remove game
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Handle premoderation (mentor approval/rejection)
    /// </summary>
    SetStatusModeration = 4,

    /// <summary>
    /// Move game to draft
    /// </summary>
    SetStatusDraft = 5,

    /// <summary>
    /// Move game to active
    /// </summary>
    SetStatusActive = 7,

    /// <summary>
    /// Close the game
    /// </summary>
    SetStatusClosed = 10,

    /// <summary>
    /// Read game commentaries
    /// </summary>
    ReadComments = 11,

    /// <summary>
    /// Post game commentaries
    /// </summary>
    CreateComment = 12,

    /// <summary>
    /// Subscribe to a game
    /// </summary>
    Subscribe = 13,

    /// <summary>
    /// Unsubscribe from a game
    /// </summary>
    Unsubscribe = 14,

    /// <summary>
    /// Create game character
    /// </summary>
    CreateCharacter = 15,

    /// <summary>
    /// Invite player to the game
    /// </summary>
    InvitePlayer = 16,

    /// <summary>
    /// Invite reader to the game
    /// </summary>
    InviteReader = 17,

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    CancelInvitation = 18
}