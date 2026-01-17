using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Community;

/// <summary>
/// API model for poll
/// </summary>
public class Poll
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Poll type
    /// </summary>
    public PollType PollType { get; set; }

    /// <summary>
    /// End date (UTC)
    /// </summary>
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Poll question
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Poll answer options
    /// </summary>
    public IEnumerable<PollOption> Options { get; set; }
}

/// <summary>
/// API model for poll option
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
    public string Text { get; set; }

    /// <summary>
    /// Votes given for the answer
    /// </summary>
    public int VotesCount { get; set; }

    /// <summary>
    /// Current user has voted for it
    /// </summary>
    public bool? Voted { get; set; }
}