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

    /// <summary>Come back after leaving: Retired to Active. The player.</summary>
    Return = 6,
}
