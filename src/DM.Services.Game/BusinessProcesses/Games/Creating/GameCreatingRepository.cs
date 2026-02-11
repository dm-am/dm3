using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;
using DbTag = DM.Services.DataAccess.BusinessObjects.Games.Links.GameTag;

namespace DM.Services.Game.BusinessProcesses.Games.Creating;

/// <inheritdoc />
internal class GameCreatingRepository : IGameCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<GameExtended> Create(DbGame game, DbRoom room,
        IEnumerable<DbTag> tags)
    {
        await _dbContext.Games.AddAsync(game);
        await _dbContext.Rooms.AddAsync(room);
        await _dbContext.GameTags.AddRangeAsync(tags);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Games
            .Where(g => g.GameId == game.GameId)
            .ProjectTo<GameExtended>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}