namespace DM.Domain.Core.Enums;

/// <summary>
/// Where a poll stands relative to now.
/// </summary>
/// <remarks>
/// Derived from the two dates rather than stored; an enum so the binder refuses
/// an unknown filter.
/// </remarks>
public enum PollStatus
{
    /// <summary>Not open yet: now is before the start.</summary>
    Pending = 0,

    /// <summary>Open: the start has passed and the end has not.</summary>
    Active = 1,

    /// <summary>Over: the end has passed.</summary>
    Closed = 2
}
