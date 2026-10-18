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
    /// <remarks>
    /// The game's comment count and its last-comment pointer travel with the mark,
    /// and the mark is conditional on the comment still being there: the answer
    /// says whether this call is the removal that happened, so a caller that has
    /// counters or events of its own does not repeat them for a comment somebody
    /// else removed first.
    /// </remarks>
    /// <param name="deleteComment">Comment deletion entity</param>
    /// <returns>Whether this call is the one that removed the comment</returns>
    Task<bool> Delete(DeleteGameCommentEntity deleteComment);

    #endregion
}
