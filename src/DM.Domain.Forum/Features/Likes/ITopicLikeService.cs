using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Likes;

/// <summary>
/// Service for topic likes
/// </summary>
public interface ITopicLikeService
{
    /// <summary>
    /// Create new like from current user to selected topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <returns>User who liked the topic</returns>
    Task<GeneralUser> LikeTopic(Guid topicId);

    /// <summary>
    /// Remove existing like from current user to selected topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <returns></returns>
    Task UnlikeTopic(Guid topicId);

    /// <summary>
    /// Create new like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeComment(Guid commentId);

    /// <summary>
    /// Remove existing like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task UnlikeComment(Guid commentId);
}
