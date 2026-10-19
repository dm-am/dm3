namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Requested change to a character's place in the game
/// </summary>
/// <remarks>
/// The caller names the intent: Retired is reached by dying, leaving and exile, each a different right.
/// A way back undoes a move of the lead; nobody brings back a character its player took out (Leave).
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

    /// <summary>Undo an exile: Retired to Active. Game lead.</summary>
    Readmit = 7,
}
