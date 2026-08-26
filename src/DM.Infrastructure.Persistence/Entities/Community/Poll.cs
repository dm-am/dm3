using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL model for poll
/// </summary>
[Table("Polls")]
public class Poll : IRemovable
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid PollId { get; set; }

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
    /// Options
    /// </summary>
    public List<PollOption> Options { get; set; } = [];

    /// <summary>
    /// Whether poll is anonymous (votes are hidden)
    /// </summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>
    /// Removed flag
    /// </summary>
    public bool IsRemoved { get; set; }
}

/// <summary>
/// DAL model for poll option
/// </summary>
[Table("PollOptions")]
public class PollOption
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid PollOptionId { get; set; }

    /// <summary>
    /// Owning poll identifier
    /// </summary>
    public Guid PollId { get; set; }

    /// <summary>
    /// Answer text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Order index within the poll. The order used to be held by the position in
    /// the document's array; a table needs it spelled out.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Votes cast for this option
    /// </summary>
    public List<PollVote> Votes { get; set; } = [];
}

/// <summary>
/// DAL model for one vote. The primary key (PollId, UserId) is the rule "one
/// voter, one option": a second vote is refused by the key, not by a read
/// before the write.
/// </summary>
[Table("PollVotes")]
public class PollVote
{
    /// <summary>
    /// Poll identifier, part of the primary key
    /// </summary>
    public Guid PollId { get; set; }

    /// <summary>
    /// Voter identifier, part of the primary key
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Chosen option identifier
    /// </summary>
    public Guid PollOptionId { get; set; }

    /// <summary>
    /// Moment the vote was cast (UTC)
    /// </summary>
    public DateTimeOffset VotedUtc { get; set; }
}
