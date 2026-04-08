using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// Query parameters for polls list
/// </summary>
public class PollsQuery : PagingQuery
{
    /// <summary>
    /// Filter by status: "pending", "active", "closed", or null for all
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Search by title and details
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by minimum start date (inclusive)
    /// </summary>
    public DateTimeOffset? StartsFrom { get; set; }

    /// <summary>
    /// Filter by maximum start date (inclusive)
    /// </summary>
    public DateTimeOffset? StartsTo { get; set; }

    /// <summary>
    /// Filter by minimum end date (inclusive)
    /// </summary>
    public DateTimeOffset? EndsFrom { get; set; }

    /// <summary>
    /// Filter by maximum end date (inclusive)
    /// </summary>
    public DateTimeOffset? EndsTo { get; set; }

    /// <summary>
    /// Sort field: "status" (default), "starts", "ends"
    /// </summary>
    public string SortBy { get; set; } = "status";

    /// <summary>
    /// Sort direction: "asc" (default) or "desc"
    /// </summary>
    public string SortOrder { get; set; } = "asc";

    /// <summary>
    /// Filter by poll type: true for anonymous, false for public, null for all
    /// </summary>
    public bool? IsAnonymous { get; set; }
}
