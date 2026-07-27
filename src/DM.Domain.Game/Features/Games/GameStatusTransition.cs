namespace DM.Domain.Game.Features.Games;

/// <summary>
/// A requested game status transition. Each value maps to exactly one legal
/// move on the status state machine; the service rejects a transition applied
/// from an incompatible current state with BadRequest.
/// </summary>
public enum GameStatusTransition
{
    /// <summary>
    /// Start: Draft -> Active (sets ActivatedUtc on first activation)
    /// </summary>
    Start = 0,

    /// <summary>
    /// Freeze: Active -> Closed with ClosedReason.Frozen
    /// </summary>
    Freeze = 1,

    /// <summary>
    /// Finish: Active -> Closed with ClosedReason.Finished
    /// </summary>
    Finish = 2,

    /// <summary>
    /// Close: Active or Closed+Frozen -> Closed with ClosedReason.None
    /// </summary>
    Close = 3,

    /// <summary>
    /// Resume / Reopen: Closed (any reason) -> Active
    /// </summary>
    Reopen = 4
}

/// <summary>
/// A requested premoderation transition for a game curated by a mentor.
/// The endpoint is gated Mentor+; the service rejects a transition applied
/// from an incompatible current premoderation state with BadRequest.
/// </summary>
public enum GamePremoderationTransition
{
    /// <summary>
    /// Submit for premoderation: AwaitingEdits -> AwaitingApproval
    /// (assigns the acting mentor as curator)
    /// </summary>
    SendToPremoderation = 0,

    /// <summary>
    /// Release from premoderation: AwaitingApproval -> Approved
    /// (clears the curator, game becomes publicly visible)
    /// </summary>
    RemoveFromPremoderation = 1
}
