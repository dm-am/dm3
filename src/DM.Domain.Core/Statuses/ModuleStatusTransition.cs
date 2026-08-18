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
/// A requested move on the module premoderation state machine. There are exactly
/// three, and they do not share an actor: two of them are the moderation verdict
/// and belong to a mentor or anybody above one, the third is the author asking
/// for that verdict.
/// </summary>
/// <remarks>
/// The pair of moves this enum used to name — a mentor taking a module in and
/// then releasing it — described a machine the module never entered: nothing
/// wrote a premoderation status at creation, so every module was born approved
/// and the only way into premoderation was a mentor putting it there by hand.
/// The rule the three below implement starts the other way round: a newbie's
/// module is born AwaitingEdits and the author is the one who moves it on.
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
