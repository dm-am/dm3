using System.Collections.Generic;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Response for discussion endpoints with comments and metadata
/// </summary>
/// <remarks>
/// Pages the same way every other list does. It used to carry its own Paging
/// DTO, and the contract published two paging schemas whose <c>number</c> meant
/// different things: the ordinal of an entity here, the page number in
/// PagingInfo. Both travelled, so a shared pagination component could not be
/// written — read <c>number</c> as a page and the discussion answered with the
/// index of a comment.
/// </remarks>
public class DiscussionResponse
{
    /// <summary>
    /// Creates a new discussion response
    /// </summary>
    public DiscussionResponse(
        IEnumerable<DiscussionComment> comments,
        PagingInfo paging,
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
    public PagingInfo Paging { get; }

    /// <summary>
    /// Total number of likes across all comments in this discussion
    /// </summary>
    public int TotalLikes { get; }

    /// <summary>
    /// Whether the current user can add comments to this discussion
    /// </summary>
    public bool CanComment { get; }
}
