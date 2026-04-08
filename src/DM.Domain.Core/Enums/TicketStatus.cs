using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Status of a moderation ticket
/// </summary>
public enum TicketStatus
{
    /// <summary>
    /// Ticket is new and awaiting review
    /// </summary>
    [Description("Открыт")]
    Open = 0,

    /// <summary>
    /// Ticket is being reviewed by a moderator
    /// </summary>
    [Description("В работе")]
    InProgress = 1,

    /// <summary>
    /// Ticket has been resolved with action taken
    /// </summary>
    [Description("Решен")]
    Resolved = 2,

    /// <summary>
    /// Ticket was closed without action (invalid report, etc.)
    /// </summary>
    [Description("Отклонен")]
    Rejected = 3
}
