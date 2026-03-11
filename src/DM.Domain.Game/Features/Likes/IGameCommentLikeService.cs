using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Likes;

/// <summary>
/// Service for game comment likes
/// </summary>
public interface IGameCommentLikeService
{
    /// <summary>
    /// Create new like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeCommentAsync(Guid commentId);

    /// <summary>
    /// Remove existing like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task UnlikeCommentAsync(Guid commentId);
}
