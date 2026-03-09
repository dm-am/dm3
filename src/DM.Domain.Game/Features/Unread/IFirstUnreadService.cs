using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Unread;

/// <summary>
/// Service for finding first unread posts and comments in games
/// </summary>
public interface IFirstUnreadService
{
    /// <summary>
    /// Find the first unread post in a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Result with location of the first unread post</returns>
    Task<FirstUnreadPostResult> GetFirstUnreadPost(Guid gameId);

    /// <summary>
    /// Find the first unread comment in a game
    /// </summary>
    /// <returns>Result with location of the first unread comment</returns>
    Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId);
}
