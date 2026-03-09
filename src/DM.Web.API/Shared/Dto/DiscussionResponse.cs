using System.Collections.Generic;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Response for discussion endpoints with comments and metadata
/// </summary>
public class DiscussionResponse
{
    /// <summary>
    /// Creates a new discussion response
    /// </summary>
    public DiscussionResponse(
        IEnumerable<DiscussionComment> comments,
        Paging paging,
        int totalLikes,
        bool canComment)
    {
        Comments = comments;
        Paging = paging;
        TotalLikes = totalLikes;
        CanComment = canComment;
    }

    /// <summary>
    /// List of comments in the discussion
    /// </summary>
    public IEnumerable<DiscussionComment> Comments { get; }

    /// <summary>
    /// Paging information
    /// </summary>
    public Paging Paging { get; }

    /// <summary>
    /// Total number of likes across all comments in this discussion
    /// </summary>
    public int TotalLikes { get; }

    /// <summary>
    /// Whether the current user can add comments to this discussion
    /// </summary>
    public bool CanComment { get; }
}
