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
    /// <returns></returns>
    Task<IEnumerable<GeneralUser>> Get(Guid boardId);
}
