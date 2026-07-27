namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// A requested blog status transition. Each value maps to exactly one legal
/// move on the status state machine; the service rejects a transition applied
/// from an incompatible current state with BadRequest.
/// Mirrors GameStatusTransition from the game module one-to-one.
/// </summary>
public enum BlogStatusTransition
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
