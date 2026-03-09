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
    Task<int> Count(Guid gameId, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get comments with paging
    /// </summary>
    Task<IEnumerable<Comment>> Get(Guid gameId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get single comment
    /// </summary>
    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Get comment for delete
    /// </summary>
    Task<GameCommentToDelete?> GetForDelete(Guid commentId);

    /// <summary>
    /// Get second last comment ID
    /// </summary>
    Task<Guid?> GetSecondLastCommentId(Guid gameId);

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
