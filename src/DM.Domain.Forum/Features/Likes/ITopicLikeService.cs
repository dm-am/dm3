using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Likes;

public interface ITopicLikeService
{
    /// <returns>User who liked the topic</returns>
    Task<GeneralUser> LikeTopicAsync(Guid topicId);

    Task UnlikeTopicAsync(Guid topicId);

    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeCommentAsync(Guid commentId);

    Task UnlikeCommentAsync(Guid commentId);
}
