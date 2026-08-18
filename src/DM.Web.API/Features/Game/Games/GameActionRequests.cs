using DM.Domain.Core.Statuses;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Request body for a game status transition
/// </summary>
public class GameStatusChangeRequest
{
    /// <summary>
    /// Requested status transition (Start / Freeze / Finish / Close / Reopen)
    /// </summary>
    public ModuleStatusTransition Transition { get; set; }
}

/// <summary>
/// Request body for a game premoderation transition
/// </summary>
public class GamePremoderationChangeRequest
{
    /// <summary>
    /// Requested premoderation transition (SetApproved / SetAwaitingEdits by a
    /// mentor, SubmitForApproval by the master)
    /// </summary>
    public ModulePremoderationTransition Transition { get; set; }
}
