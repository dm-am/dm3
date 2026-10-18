namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Requested change to a character's place in the game
/// </summary>
/// <remarks>
/// The caller names the intent. The status alone does not carry it: Retired is
/// reached by dying, by leaving and by being exiled, and each of the three is a
/// different right held by a different person. The API used to take the target
/// status with three booleans beside it and guess, which meant a request that
/// named no reason had no arm to land in and answered 500.
///
/// Same shape as GameStatusTransition, for the same reason.
///
/// Every way back into play undoes a move of the lead: Accept undoes Decline,
/// Resurrect undoes Kill, Readmit undoes Exile. A character its player took out
/// of the game (Leave) is brought back by nobody, the player, the lead and the
/// moderation alike.
/// </remarks>
public enum CharacterStatusTransition
{
    /// <summary>Accept into the game: UnderReview or Declined to Active. Game lead.</summary>
    Accept = 0,

    /// <summary>Refuse the application: UnderReview to Declined. Game lead.</summary>
    Decline = 1,

    /// <summary>The character died: Active to Retired. Game lead.</summary>
    Kill = 2,

    /// <summary>The character is thrown out: Active to Retired. Game lead.</summary>
    Exile = 3,

    /// <summary>The player walks away: Active to Retired. The player.</summary>
    Leave = 4,

    /// <summary>Undo a death: Retired to Active. Game lead.</summary>
    Resurrect = 5,

    // 6 was Return, the player bringing back a character he took out of the
    // game. Removed: leaving is final.

    /// <summary>Undo an exile: Retired to Active. Game lead.</summary>
    Readmit = 7,
}
