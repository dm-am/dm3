using System;
using DM.Services.Common.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Dto;

/// <summary>
/// DTO to remove the comment from publication
/// </summary>
internal class CommentToDelete : Comment
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId { get; set; }

    /// <summary>
    /// Current comment count of the parent publication
    /// </summary>
    public int EntityCommentCount { get; set; }
}
