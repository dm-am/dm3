using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Dto.Output;
using Game = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Gaming.BusinessProcesses.Games.Updating
{
    /// <inheritdoc />
    public class GameUpdatingRepository : IGameUpdatingRepository
    {
        private readonly DmDbContext dbContext;
        private readonly IMapper mapper;

        /// <inheritdoc />
        public GameUpdatingRepository(
            DmDbContext dbContext,
            IMapper mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
        }
        
        /// <inheritdoc />
        public async Task<GameExtended> Update(IUpdateBuilder<Game> updateGame, Token assistantAssignmentToken)
        {
            var gameId = updateGame.AttachTo(dbContext);
            dbContext.Tokens.Add(assistantAssignmentToken);
            await dbContext.SaveChangesAsync();

            return dbContext.Games
                .Where(g => g.GameId == gameId)
                .ProjectTo<GameExtended>(mapper.ConfigurationProvider)
                .First();
        }
    }
}