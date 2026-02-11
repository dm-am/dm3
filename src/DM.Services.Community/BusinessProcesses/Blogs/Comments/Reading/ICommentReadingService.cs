using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;

/// <summary>
/// Service for reading publication comments
/// </summary>
public interface ICommentReadingService
{
    /// <summary>
    /// Get list of publication comments by publication id
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Pair of comments list and paging data</returns>
    Task<(IEnumerable<Comment> comments, PagingResult paging)> Get(Guid publicationId, PagingQuery query);

    /// <summary>
    /// Get publication comment by id
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Found comment</returns>
    Task<Comment> Get(Guid commentId);

    /// <summary>
    /// Mark all publication comments as read
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns></returns>
    Task MarkAsRead(Guid publicationId);

    /// <summary>
    /// Mark all blog comments as read
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <returns></returns>
    Task MarkBlogCommentsAsRead(Guid blogId);
}
