using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Comments;

public interface IGameCommentService
{
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// True exactly when <see cref="CreateAsync" /> takes a comment, the text aside:
    /// the page offers the form by this answer instead of refusing what was typed.
    /// </summary>
    /// <param name="game">The game, read for the current user</param>
    bool MayCreate(Games.Game game);

    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid gameId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Refused the way the listing refuses; a comment of another kind of discussion is not found
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    Task<Comment> UpdateAsync(UpdateComment updateComment);

    /// <summary>
    /// Soft delete
    /// </summary>
    Task DeleteAsync(Guid commentId);

    Task MarkAsReadAsync(Guid gameId);
}
