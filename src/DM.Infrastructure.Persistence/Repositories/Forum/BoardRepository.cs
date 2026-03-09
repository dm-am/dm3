using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class BoardRepository(
    DmDbContext dmDbContext,
    ICache cache,
    IConfigurationProvider mapperConfig)
    : IBoardRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Board>> SelectBoards(BoardAccessPolicy? accessPolicy)
    {
        var boards = await cache.GetOrCreate<Board[]>("Boards", async () => await dmDbContext.Boards
            .TagWith("DM.Forum.BoardsList")
            .OrderBy(b => b.Order)
            .ProjectTo<Board>(mapperConfig)
            .ToArrayAsync());

        if (!accessPolicy.HasValue)
        {
            return boards;
        }

        return boards.Where(b => (b.ViewPolicy & accessPolicy) != BoardAccessPolicy.None).ToArray();
    }
}
