using System;
using System.Collections.Generic;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// DTO model for poll
/// </summary>
public class Poll
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Start moment (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// End moment (UTC)
    /// </summary>
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Question text
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Whether poll is anonymous (votes are hidden)
    /// </summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>
    /// Answers list
    /// </summary>
    public IEnumerable<PollOption> Options { get; set; } = [];
}

/// <summary>
/// DTO model for poll answer option
/// </summary>
public class PollOption
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Answer text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// List of voted users
    /// </summary>
    public IEnumerable<Guid> UserIds { get; set; } = [];
}
