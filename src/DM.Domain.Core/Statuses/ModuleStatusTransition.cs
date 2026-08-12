namespace DM.Domain.Core.Statuses;

/// <summary>
/// A requested move on the module status state machine. Each value is exactly
/// one move; a move applied from a state it is not legal in is refused with
/// BadRequest.
/// </summary>
/// <remarks>
/// One vocabulary for a game and a blog, because it is one machine. Each module
/// used to declare a member-for-member copy of this enum beside a copy of the
/// rules that read it. The numbering and the member names are kept from those
/// copies: the name is what travels on the wire.
/// </remarks>
public enum ModuleStatusTransition
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
/// A requested move on the module premoderation state machine. The endpoints
/// that accept it are gated Mentor+; a move applied from an incompatible
/// premoderation status is refused with BadRequest.
/// </summary>
public enum ModulePremoderationTransition
{
    /// <summary>
    /// Submit for premoderation: AwaitingEdits -> AwaitingApproval
    /// (assigns the acting mentor as curator)
    /// </summary>
    SendToPremoderation = 0,

    /// <summary>
    /// Release from premoderation: AwaitingApproval -> Approved
    /// (clears the curator, the module becomes publicly visible)
    /// </summary>
    RemoveFromPremoderation = 1
}
