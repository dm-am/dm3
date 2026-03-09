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
    /// Start moment
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End moment
    /// </summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// Question text
    /// </summary>
    public string Title { get; set; } = null!;

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
