using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
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
    /// Get topic comments
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Comments list with paging</returns>
    Task<(IEnumerable<Comment> Comments, PagingInfo Paging)> Get(Guid topicId, PagingQuery query);

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
    /// <returns></returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="comment">Comment DTO model</param>
    /// <returns></returns>
    Task<Envelope<Comment>> Update(Guid commentId, Comment comment);

    /// <summary>
    /// Delete comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task Delete(Guid commentId);
}
