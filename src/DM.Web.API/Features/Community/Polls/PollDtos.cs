using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Community.Polls;

/// <summary>
/// Poll information
/// </summary>
public class Poll
{
    /// <summary>
    /// Poll unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Poll type (Community, Topic, etc.)
    /// </summary>
    public PollType PollType { get; set; }

    /// <summary>
    /// Poll end date and time (UTC)
    /// </summary>
    [JsonPropertyName("ends")]
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Poll question/title
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Available answer options
    /// </summary>
    public IEnumerable<PollOption> Options { get; set; } = Array.Empty<PollOption>();
}

/// <summary>
/// Poll answer option
/// </summary>
public class PollOption
{
    /// <summary>
    /// Option unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Option text
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Total number of votes for this option
    /// </summary>
    public int VotesCount { get; set; }

    /// <summary>
    /// Whether the current user has voted for this option (null if not authenticated)
    /// </summary>
    public bool? Voted { get; set; }
}

/// <summary>
/// Request to create a new poll
/// </summary>
public class CreatePollRequest
{
    /// <summary>
    /// Poll question/title (5-500 characters)
    /// </summary>
    /// <example>What's your favorite programming language?</example>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 500 characters")]
    public string Title { get; set; } = "";

    /// <summary>
    /// Poll type
    /// </summary>
    [Required(ErrorMessage = "Poll type is required")]
    public PollType PollType { get; set; }

    /// <summary>
    /// Poll duration in days (1-365)
    /// </summary>
    [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365 days")]
    public int DurationDays { get; set; } = 7;

    /// <summary>
    /// Answer options (2-10 options required)
    /// </summary>
    [Required(ErrorMessage = "Options are required")]
    [MinLength(2, ErrorMessage = "At least 2 options are required")]
    [MaxLength(10, ErrorMessage = "Maximum 10 options allowed")]
    public List<string> Options { get; set; } = new();
}

/// <summary>
/// Request to update an existing poll
/// </summary>
public class UpdatePollRequest
{
    /// <summary>
    /// Updated poll title (5-500 characters, optional)
    /// </summary>
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 500 characters")]
    public string? Title { get; set; }

    /// <summary>
    /// New end date and time (UTC, optional)
    /// </summary>
    /// <remarks>
    /// Must be in the future and cannot be shortened if poll has votes
    /// </remarks>
    [JsonPropertyName("ends")]
    public DateTimeOffset? EndsUtc { get; set; }
}

/// <summary>
/// Request to vote on a poll
/// </summary>
public class VoteRequest
{
    /// <summary>
    /// Option ID to vote for
    /// </summary>
    [Required(ErrorMessage = "Option ID is required")]
    public Guid OptionId { get; set; }
}

/// <summary>
/// Query parameters for listing polls
/// </summary>
public class PollsQuery : PagingQuery
{
    /// <summary>
    /// Only get active polls
    /// </summary>
    public bool OnlyActive { get; set; }
}
