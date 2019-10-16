using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;
using DbTag = DM.Services.DataAccess.BusinessObjects.Games.Links.GameTag;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating
{
    /// <inheritdoc />
    public class GameCreatingRepository : IGameCreatingRepository
    {
        private readonly DmDbContext dbContext;
        private readonly IMapper mapper;

        /// <inheritdoc />
        public GameCreatingRepository(
            DmDbContext dbContext,
            IMapper mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
        }
        
        /// <inheritdoc />
        public async Task<GameExtended> Create(DbGame game, DbRoom room,
            IEnumerable<DbTag> tags, Token assistantAssignmentToken)
        {
            await Task.WhenAll(
                dbContext.Games.AddAsync(game),
                dbContext.Rooms.AddAsync(room),
                dbContext.GameTags.AddRangeAsync(tags),
                dbContext.Tokens.AddAsync(assistantAssignmentToken));
            await dbContext.SaveChangesAsync();
            return await dbContext.Games
                .Where(g => g.GameId == game.GameId)
                .ProjectTo<GameExtended>(mapper.ConfigurationProvider)
                .FirstAsync();
        }
    }
}