using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// API service for forum commentaries (global operations)
/// </summary>
public interface IForumCommentApiService
{
    /// <summary>
    /// Get topic discussion with permission flags
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Discussion response with comments and metadata</returns>
    Task<DiscussionResponse> GetDiscussion(Guid topicId, PagingQuery query);

    /// <summary>
    /// Get where the reader continues in a topic
    /// </summary>
    /// <param name="boardAlias">Board URL alias</param>
    /// <param name="topicNumber">Topic number within the board</param>
    /// <returns>Envelope of the comment to open the topic at</returns>
    Task<Envelope<FirstUnreadComment>> GetFirstUnread(string boardAlias, int topicNumber);

    /// <summary>
    /// Mark topic comments as read
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    Task MarkAsRead(Guid topicId);

    /// <summary>
    /// Mark all forum comments as read
    /// </summary>
    /// <param name="forumId">Forum identifier</param>
    Task MarkAsRead(string forumId);

    /// <summary>
    /// Mark all comments on all forums as read
    /// </summary>
    Task MarkAllAsRead();
}
