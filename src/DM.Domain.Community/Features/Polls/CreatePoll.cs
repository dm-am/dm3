using System;
using System.Collections.Generic;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// DTO model for poll creating
/// </summary>
public class CreatePoll
{
    /// <summary>
    /// Poll title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Poll start date (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Poll end date (UTC)
    /// </summary>
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Whether poll is anonymous (default: true)
    /// </summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>
    /// List of possible poll answers
    /// </summary>
    public IEnumerable<string> Options { get; set; } = [];
}