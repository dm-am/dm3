using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Comments;

/// <summary>
/// API service for game comment likes
/// </summary>
public interface IGameCommentLikeApiService
{
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
