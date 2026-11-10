using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

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

    /// <summary>
    /// Where the poll stands at the given moment. The window is half-open: the
    /// poll opens at <see cref="StartsUtc" /> and closes at <see cref="EndsUtc" />.
    /// </summary>
    /// <remarks>
    /// The one place the voting window is written down: the vote is taken and the
    /// page calls the poll open by the same answer. The storage's listing restates
    /// it in SQL, which cannot call this.
    /// </remarks>
    public PollStatus StatusAt(DateTimeOffset now)
    {
        if (now < StartsUtc)
        {
            return PollStatus.Pending;
        }

        return now >= EndsUtc ? PollStatus.Closed : PollStatus.Active;
    }
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
