using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// API service for topic comments
/// </summary>
public interface ITopicCommentApiService
{
    /// <summary>
    /// Get topic comments with filtering and sorting
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <returns>Comments list with paging</returns>
    Task<(IEnumerable<Comment> Comments, PagingInfo Paging)> Get(Guid topicId, CommentsQuery query);

    /// <summary>
    /// Create new comment
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="request">Comment creation request</param>
    /// <returns>Envelope of created comment</returns>
    Task<Envelope<Comment>> Create(Guid topicId, CreateCommentRequest request);

    /// <summary>
    /// Get comment by identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope of comment</returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="comment">Comment DTO model</param>
    /// <returns>Envelope of updated comment</returns>
    Task<Envelope<Comment>> Update(Guid commentId, Comment comment);

    /// <summary>
    /// Delete comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task Delete(Guid commentId);
}
