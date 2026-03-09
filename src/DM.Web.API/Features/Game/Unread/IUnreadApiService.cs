using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Unread;

/// <summary>
/// API service for finding first unread content in games
/// </summary>
public interface IUnreadApiService
{
    /// <summary>
    /// Get the first unread post in a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Result with location of the first unread post</returns>
    Task<Envelope<FirstUnreadPostResult>> GetFirstUnreadPost(Guid gameId);

    /// <summary>
    /// Get the first unread comment in a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Result with location of the first unread comment</returns>
    Task<Envelope<FirstUnreadCommentResult>> GetFirstUnreadComment(Guid gameId);
}
