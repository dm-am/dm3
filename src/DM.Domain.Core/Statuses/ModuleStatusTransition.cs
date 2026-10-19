namespace DM.Domain.Core.Statuses;

/// <summary>
/// A requested move on the module status state machine. Each value is exactly
/// one move; a move applied from a state it is not legal in is refused with
/// BadRequest.
/// </summary>
/// <remarks>
/// One vocabulary for a game and a blog; the member name is what travels on the wire.
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
/// A requested move on the module premoderation state machine. There are exactly
/// three, and they do not share an actor: two of them are the moderation verdict
/// and belong to a mentor or anybody above one, the third is the author asking
/// for that verdict.
/// </summary>
/// <remarks>
/// A newbie's module is born AwaitingEdits and the author is the one who moves it on.
/// </remarks>
public enum ModulePremoderationTransition
{
    /// <summary>
    /// Moderation verdict "approved": any status -> Approved (clears the curator,
    /// the module becomes publicly visible). Legal from every status, because a
    /// mentor may approve a module they never returned for edits.
    /// </summary>
    SetApproved = 0,

    /// <summary>
    /// Moderation verdict "not yet": any status -> AwaitingEdits (records the
    /// acting mentor as the curator answering for the module). Legal from every
    /// status, including Approved — a module already published can be pulled back.
    /// </summary>
    SetAwaitingEdits = 1,

    /// <summary>
    /// The author asks for a verdict: AwaitingEdits -> AwaitingApproval. The only
    /// move an author has, the only one with a legality condition, and the only
    /// one that leaves the curator untouched — the author never becomes one.
    /// </summary>
    SubmitForApproval = 2
}
