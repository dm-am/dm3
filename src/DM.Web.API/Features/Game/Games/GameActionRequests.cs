using DM.Domain.Game.Features.Games;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Request body for a game status transition
/// </summary>
public class GameStatusChangeRequest
{
    /// <summary>
    /// Requested status transition (Start / Freeze / Finish / Close / Reopen)
    /// </summary>
    public GameStatusTransition Transition { get; set; }
}

/// <summary>
/// Request body for a game premoderation transition
/// </summary>
public class GamePremoderationChangeRequest
{
    /// <summary>
    /// Requested premoderation transition (SendToPremoderation / RemoveFromPremoderation)
    /// </summary>
    public GamePremoderationTransition Transition { get; set; }
}
