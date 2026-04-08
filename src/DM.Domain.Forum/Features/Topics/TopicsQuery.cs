using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Query parameters for topic list filtering
/// </summary>
public class TopicsQuery : PagingQuery
{
    /// <summary>
    /// Filter attached/non attached (null = all topics)
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Text search by title (case-insensitive contains)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by author usernames (case-insensitive, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? Authors { get; set; }

    /// <summary>
    /// Filter by created date (from) - ISO 8601 format
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Filter by created date (to) - ISO 8601 format
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <summary>
    /// Sort field: lastActivity, created, comments, title
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }
}
