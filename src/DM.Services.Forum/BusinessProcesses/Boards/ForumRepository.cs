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
internal class ForumRepository(
    DmDbContext dmDbContext,
    ICache cache,
    IConfigurationProvider mapperConfig)
    : IForumRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Forum>> SelectFora(ForumAccessPolicy? accessPolicy)
    {
        var forums = await cache.GetOrCreate<Dto.Output.Forum[]>("Fora", async () => await dmDbContext.Boards
            .TagWith("DM.Forum.BoardsList")
            .OrderBy(f => f.Order)
            .ProjectTo<Dto.Output.Forum>(mapperConfig)
            .ToArrayAsync());

        // Фильтруем по политике доступа без создания копий объектов
        // (объекты из кэша только читаются при сериализации в JSON)
        if (!accessPolicy.HasValue)
        {
            return forums;
        }

        return forums.Where(f => (f.ViewPolicy & accessPolicy) != ForumAccessPolicy.NoOne).ToArray();
    }
}