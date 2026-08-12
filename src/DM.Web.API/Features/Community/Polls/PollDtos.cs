using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

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
    /// Poll start date and time (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Poll end date and time (UTC)
    /// </summary>
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Poll question/title
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Poll status (computed from StartsUtc and EndsUtc)
    /// </summary>
    public PollStatus Status { get; set; }

    /// <summary>
    /// Whether poll is anonymous (votes are hidden)
    /// </summary>
    public bool IsAnonymous { get; set; }

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

    /// <summary>
    /// Users who voted for this option (null for anonymous polls, max 15)
    /// </summary>
    public IEnumerable<UserRef>? Voters { get; set; }

    /// <summary>
    /// Total voters count if exceeds 15 (null otherwise)
    /// </summary>
    public int? TotalVoters { get; set; }
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
    /// Optional description/details for the poll (max 1000 characters)
    /// </summary>
    [StringLength(1000, ErrorMessage = "Details cannot exceed 1000 characters")]
    public string? Details { get; set; }

    /// <summary>
    /// Poll start date and time (UTC)
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Poll end date and time (UTC)
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>
    /// Answer options (2-10 options required)
    /// </summary>
    [Required(ErrorMessage = "Options are required")]
    [MinLength(2, ErrorMessage = "At least 2 options are required")]
    [MaxLength(10, ErrorMessage = "Maximum 10 options allowed")]
    public List<string> Options { get; set; } = new();

    /// <summary>
    /// Whether poll is anonymous (default: true)
    /// </summary>
    public bool IsAnonymous { get; set; } = true;
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
    /// Updated description/details (max 1000 characters, optional)
    /// </summary>
    [StringLength(1000, ErrorMessage = "Details cannot exceed 1000 characters")]
    public string? Details { get; set; }

    /// <summary>
    /// New start date and time (UTC, optional)
    /// </summary>
    public DateTimeOffset? StartsUtc { get; set; }

    /// <summary>
    /// New end date and time (UTC, optional)
    /// </summary>
    public DateTimeOffset? EndsUtc { get; set; }

    /// <summary>
    /// Whether poll is anonymous. Changing from anonymous to public resets all votes.
    /// </summary>
    public bool? IsAnonymous { get; set; }
}

/// <summary>
/// Query parameters for listing polls
/// </summary>
public class PollsQuery : PagingQuery
{
    /// <summary>
    /// Filter by status, or omit for all
    /// </summary>
    /// <example>active</example>
    public PollStatus? Status { get; set; }

    /// <summary>
    /// Search polls by title and details (case-insensitive substring match)
    /// </summary>
    /// <example>game</example>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by minimum start date (ISO 8601, inclusive)
    /// </summary>
    /// <example>2024-01-01T00:00:00Z</example>
    public DateTimeOffset? StartsFromUtc { get; set; }

    /// <summary>
    /// Filter by maximum start date (ISO 8601, inclusive)
    /// </summary>
    /// <example>2024-12-31T23:59:59Z</example>
    public DateTimeOffset? StartsToUtc { get; set; }

    /// <summary>
    /// Filter by minimum end date (ISO 8601, inclusive)
    /// </summary>
    /// <example>2024-01-01T00:00:00Z</example>
    public DateTimeOffset? EndsFromUtc { get; set; }

    /// <summary>
    /// Filter by maximum end date (ISO 8601, inclusive)
    /// </summary>
    /// <example>2024-12-31T23:59:59Z</example>
    public DateTimeOffset? EndsToUtc { get; set; }

    /// <summary>
    /// Sort field: "status" (default), "starts", "ends"
    /// </summary>
    /// <example>status</example>
    public string SortBy { get; set; } = "status";

    /// <summary>
    /// Sort direction: "asc" (default) or "desc"
    /// </summary>
    /// <example>asc</example>
    public string SortOrder { get; set; } = "asc";

    /// <summary>
    /// Filter by poll type: true for anonymous, false for public, null for all
    /// </summary>
    /// <example>true</example>
    public bool? IsAnonymous { get; set; }
}
