using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Likes;

/// <summary>
/// API service for topic likes
/// </summary>
public interface ITopicLikeApiService
{
    /// <summary>
    /// Like the topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <returns>Envelope for user who just liked the topic</returns>
    Task<Envelope<User>> LikeTopic(Guid topicId);

    /// <summary>
    /// Remove user's like from topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    Task UnlikeTopic(Guid topicId);

    /// <summary>
    /// Like the comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope for user who just liked the comment</returns>
    Task<Envelope<User>> LikeComment(Guid commentId);

    /// <summary>
    /// Remove user's like from comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task UnlikeComment(Guid commentId);
}
