using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Blacklists;

public interface IGameBlacklistService
{
    Task<IEnumerable<GeneralUser>> Get(Guid gameId);

    Task<GeneralUser> Add(OperateBlacklistLink operateBlacklistLink);

    Task Remove(OperateBlacklistLink operateBlacklistLink);
}
