using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Boards;

public interface IBoardModeratorRepository
{
    Task<IEnumerable<GeneralUser>> Get(Guid boardId);

    Task Add(Guid boardId, Guid userId);

    Task Remove(Guid boardId, Guid userId);

    Task<bool> IsModerator(Guid boardId, Guid userId);
}
