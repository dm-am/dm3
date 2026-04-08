using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// Board moderators storage
/// </summary>
public interface IBoardModeratorRepository
{
    /// <summary>
    /// Get list of board moderators
    /// </summary>
    /// <param name="boardId">Board id</param>
    /// <returns>List of board moderators</returns>
    Task<IEnumerable<GeneralUser>> Get(Guid boardId);

    /// <summary>
    /// Add a user as board moderator
    /// </summary>
    /// <param name="boardId">Board id</param>
    /// <param name="userId">User id to add as moderator</param>
    /// <returns>Task</returns>
    Task Add(Guid boardId, Guid userId);

    /// <summary>
    /// Remove a user from board moderators
    /// </summary>
    /// <param name="boardId">Board id</param>
    /// <param name="userId">User id to remove from moderators</param>
    /// <returns>Task</returns>
    Task Remove(Guid boardId, Guid userId);

    /// <summary>
    /// Check if a user is a board moderator
    /// </summary>
    /// <param name="boardId">Board id</param>
    /// <param name="userId">User id to check</param>
    /// <returns>True if user is a moderator of the board</returns>
    Task<bool> IsModerator(Guid boardId, Guid userId);
}
