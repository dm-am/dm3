using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Comments;

/// <summary>
/// Repository for game comment operations
/// </summary>
public interface IGameCommentRepository
{
    #region Read

    /// <summary>
    /// Count comments in game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Query parameters for filtering</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from count</param>
    Task<int> Count(Guid gameId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get comments with paging
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Query parameters for filtering and sorting</param>
    /// <param name="paging">Paging data</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<IEnumerable<Comment>> Get(Guid gameId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get single comment
    /// </summary>
    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Get comment for delete
    /// </summary>
    Task<GameCommentToDelete?> GetForDelete(Guid commentId);

    /// <summary>
    /// The newest live comment of the game other than <paramref name="exceptCommentId" />:
    /// the successor of the comment being deleted.
    /// </summary>
    /// <remarks>
    /// Named by exclusion rather than by position — see the note on the forum's
    /// counterpart. Two comments can share a timestamp, and then "the second one down the
    /// list" can be the row that is leaving, which would leave the game pointing at a
    /// soft-deleted comment.
    /// </remarks>
    Task<Guid?> GetNewestCommentIdExcept(Guid gameId, Guid exceptCommentId);

    #endregion

    #region Write

    /// <summary>
    /// Create comment
    /// </summary>
    Task<Comment> Create(CreateGameCommentEntity createComment);

    /// <summary>
    /// Update comment
    /// </summary>
    Task<Comment> Update(UpdateGameCommentEntity updateComment);

    /// <summary>
    /// Delete comment
    /// </summary>
    Task Delete(DeleteGameCommentEntity deleteComment);

    #endregion
}
