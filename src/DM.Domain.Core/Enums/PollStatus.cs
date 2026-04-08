using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Poll status based on StartsUtc and EndsUtc dates
/// </summary>
public enum PollStatus
{
    /// <summary>
    /// Poll has not started yet (now &lt; StartsUtc)
    /// </summary>
    [Description("Ожидает начала")]
    Pending = 0,

    /// <summary>
    /// Poll is currently active (StartsUtc &lt;= now &lt; EndsUtc)
    /// </summary>
    [Description("Активен")]
    Active = 1,

    /// <summary>
    /// Poll has ended (now &gt;= EndsUtc)
    /// </summary>
    [Description("Завершён")]
    Closed = 2
}
