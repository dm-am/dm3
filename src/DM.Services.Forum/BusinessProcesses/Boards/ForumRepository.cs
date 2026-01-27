using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Boards;

/// <inheritdoc />
internal class BoardRepository(
    DmDbContext dmDbContext,
    ICache cache,
    IConfigurationProvider mapperConfig)
    : IBoardRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Board>> SelectBoards(BoardAccessPolicy? accessPolicy)
    {
        var boards = await cache.GetOrCreate<Dto.Output.Board[]>("Boards", async () => await dmDbContext.Boards
            .TagWith("DM.Forum.BoardsList")
            .OrderBy(b => b.Order)
            .ProjectTo<Dto.Output.Board>(mapperConfig)
            .ToArrayAsync());

        // Фильтруем по политике доступа без создания копий объектов
        // (объекты из кэша только читаются при сериализации в JSON)
        if (!accessPolicy.HasValue)
        {
            return boards;
        }

        return boards.Where(b => (b.ViewPolicy & accessPolicy) != BoardAccessPolicy.NoOne).ToArray();
    }
}