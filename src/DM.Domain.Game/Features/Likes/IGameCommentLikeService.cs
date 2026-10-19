using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Likes;

public interface IGameCommentLikeService
{
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeCommentAsync(Guid commentId);

    Task UnlikeCommentAsync(Guid commentId);
}
