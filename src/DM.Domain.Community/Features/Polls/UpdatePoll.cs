using System;

namespace DM.Domain.Community.Features.Polls;

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
    public string? Title { get; set; }

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Poll start date (UTC)
    /// </summary>
    public DateTimeOffset? StartsUtc { get; set; }

    /// <summary>
    /// Poll end date (UTC)
    /// </summary>
    public DateTimeOffset? EndsUtc { get; set; }

    /// <summary>
    /// Whether poll is anonymous. Changing from anonymous to public resets all votes.
    /// </summary>
    public bool? IsAnonymous { get; set; }
}
