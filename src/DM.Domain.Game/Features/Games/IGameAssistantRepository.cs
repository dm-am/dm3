using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Games;

public interface IGameAssistantRepository
{
    Task<IEnumerable<GeneralUser>> GetAssistants(Guid gameId);

    Task<bool> IsAssistantByUsername(Guid gameId, string username);

    Task RemoveAssistantByUsername(Guid gameId, string username);
}
