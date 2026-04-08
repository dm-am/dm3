using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <summary>
/// Query parameters for publication comment list filtering
/// </summary>
public class PublicationCommentsQuery : PagingQuery
{
    /// <summary>
    /// Text search by comment content (case-insensitive contains)
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
    /// Sort field: created (default), likes
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc (default for created) or desc
    /// </summary>
    public string? SortOrder { get; set; }
}
