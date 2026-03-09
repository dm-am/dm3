using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Comments;

/// <summary>
/// Unified service for game comments
/// </summary>
public interface IGameCommentService
{
    /// <summary>
    /// Create new comment for game
    /// </summary>
    Task<Comment> Create(CreateComment createComment);

    /// <summary>
    /// Get comments list for game with paging
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Paging query</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> Get(Guid gameId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get single comment by identifier
    /// </summary>
    Task<Comment> Get(Guid commentId);

    /// <summary>
    /// Update comment
    /// </summary>
    Task<Comment> Update(UpdateComment updateComment);

    /// <summary>
    /// Delete comment (soft delete)
    /// </summary>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all comments as read for game
    /// </summary>
    Task MarkAsRead(Guid gameId);
}
