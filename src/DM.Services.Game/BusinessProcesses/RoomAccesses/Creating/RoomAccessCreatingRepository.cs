using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using RoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <inheritdoc />
internal class RoomAccessCreatingRepository : IRoomAccessCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomAccessCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Dto.Output.RoomAccess> Create(RoomAccess access)
    {
        _dbContext.RoomAccesses.Add(access);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.RoomAccesses
            .Where(l => l.AccessId == access.AccessId)
            .ProjectTo<Dto.Output.RoomAccess>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> FindReaderId(Guid gameId, string readerLogin)
    {
        var readerWrapper = await _dbContext.Readers
            .Where(r => r.User.Login == readerLogin && r.GameId == gameId)
            .Select(r => new {r.ReaderId})
            .FirstOrDefaultAsync();
        return readerWrapper?.ReaderId;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindCharacterGameId(Guid characterId)
    {
        var gameWrapper = await _dbContext.Characters
            .Where(c => c.CharacterId == characterId)
            .Select(c => new {c.GameId})
            .FirstOrDefaultAsync();
        return gameWrapper?.GameId;
    }
}