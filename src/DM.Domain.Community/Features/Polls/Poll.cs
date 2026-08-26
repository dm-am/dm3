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

    /// <summary>
    /// Where the poll stands at the given moment. The window is half-open: the
    /// poll opens at <see cref="StartsUtc" /> and closes at <see cref="EndsUtc" />.
    /// </summary>
    /// <remarks>
    /// The one place the voting window is written down. A vote and an unvote are
    /// accepted exactly while this answers <see cref="PollStatus.Active" />, and
    /// the response reports the same value to the reader, so the poll a page
    /// calls open is the poll that takes the vote. The two halves used to be
    /// separate comparisons in separate projects, and a boundary moved in one of
    /// them would have offered a vote the authorization then refused.
    ///
    /// The listing in the storage still restates the same window twice - once as
    /// a filter, once as a CASE ladder for the status sort - because those are
    /// translated into SQL rather than called.
    /// </remarks>
    /// <param name="now">Moment to judge the poll at</param>
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
