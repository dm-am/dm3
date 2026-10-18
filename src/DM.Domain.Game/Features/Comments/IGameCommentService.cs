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
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Whether the current user may write a new comment in the game's
    /// discussion: true exactly when <see cref="CreateAsync" /> takes one, the
    /// text aside. The page offers the form by this answer instead of refusing
    /// what was typed.
    /// </summary>
    /// <param name="game">The game, read for the current user</param>
    bool MayCreate(Games.Game game);

    /// <summary>
    /// Get comments list for game with paging
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid gameId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment of a game by ID, refused the way the game's
    /// comment listing refuses the current user. A comment of any other kind of
    /// discussion is not found
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    /// <summary>
    /// Update comment
    /// </summary>
    Task<Comment> UpdateAsync(UpdateComment updateComment);

    /// <summary>
    /// Delete comment (soft delete)
    /// </summary>
    Task DeleteAsync(Guid commentId);

    /// <summary>
    /// Mark all comments as read for game
    /// </summary>
    Task MarkAsReadAsync(Guid gameId);
}
