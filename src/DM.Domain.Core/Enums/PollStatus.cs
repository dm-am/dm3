namespace DM.Domain.Core.Enums;

/// <summary>
/// Where a poll stands relative to now.
/// </summary>
/// <remarks>
/// Derived from the two dates rather than stored: a poll starts and closes by
/// the clock, so there is no state to keep in step. Named here because the
/// listing filters by it, and a filter spelled as a free string answered an
/// unrecognised word with every poll on the site.
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
