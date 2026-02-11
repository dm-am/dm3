using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

/// <inheritdoc />
internal class GameUpdatingRepository : IGameUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<GameExtended> Update(IUpdateBuilder<DbGame> updateGame)
    {
        var gameId = updateGame.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();

        return _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameExtended>(_mapper.ConfigurationProvider)
            .First();
    }
}