using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Likes;

public interface ITopicLikeService
{
    Task<GeneralUser> LikeTopicAsync(Guid topicId);

    Task UnlikeTopicAsync(Guid topicId);

    Task<GeneralUser> LikeCommentAsync(Guid commentId);

    Task UnlikeCommentAsync(Guid commentId);
}
