using System;

namespace DM.Services.Community.BusinessProcesses.Polls.Updating;

/// <summary>
/// DTO model for poll updating
/// </summary>
public class UpdatePoll
{
    /// <summary>
    /// Poll identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Poll title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Desired poll end date
    /// </summary>
    public DateTimeOffset? EndDate { get; set; }
}
