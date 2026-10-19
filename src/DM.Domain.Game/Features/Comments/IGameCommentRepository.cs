using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Comments;

public interface IGameCommentRepository
{
    Task<int> Count(Guid gameId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<IEnumerable<Comment>> Get(Guid gameId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    Task<Comment?> Get(Guid commentId);

    Task<GameCommentToDelete?> GetForDelete(Guid commentId);

    Task<Comment> Create(CreateGameCommentEntity createComment);

    Task<Comment> Update(UpdateGameCommentEntity updateComment);

    /// <remarks>
    /// The game's comment count and last-comment pointer move with the mark, in one transaction.
    /// </remarks>
    /// <returns>Whether this call removed the comment, so a caller does not repeat its own counters and events</returns>
    Task<bool> Delete(DeleteGameCommentEntity deleteComment);
}
