using DM.Domain.Game.Features.Characters;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// Request body for a character status transition
/// </summary>
public class CharacterStatusChangeRequest
{
    /// <summary>
    /// Requested transition (Accept / Decline / Kill / Exile / Leave / Resurrect / Return)
    /// </summary>
    public CharacterStatusTransition Transition { get; set; }
}
