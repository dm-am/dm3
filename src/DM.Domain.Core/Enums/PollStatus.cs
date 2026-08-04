namespace DM.Domain.Core.Enums;

/// <summary>
/// Poll status based on StartsUtc and EndsUtc dates
/// </summary>
public enum PollStatus
{
    /// <summary>
    /// Poll has not started yet (now &lt; StartsUtc)
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Poll is currently active (StartsUtc &lt;= now &lt; EndsUtc)
    /// </summary>
    Active = 1,

    /// <summary>
    /// Poll has ended (now &gt;= EndsUtc)
    /// </summary>
    Closed = 2
}
