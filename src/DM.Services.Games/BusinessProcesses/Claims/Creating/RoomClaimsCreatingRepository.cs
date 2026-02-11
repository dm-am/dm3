using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using RoomClaim = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomClaim;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Creating;

/// <inheritdoc />
internal class RoomClaimsCreatingRepository : IRoomClaimsCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomClaimsCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Dto.Output.RoomClaim> Create(RoomClaim claim)
    {
        _dbContext.RoomClaims.Add(claim);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.RoomClaims
            .Where(l => l.RoomClaimId == claim.RoomClaimId)
            .ProjectTo<Dto.Output.RoomClaim>(_mapper.ConfigurationProvider)
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
            .Where(c => c.CharacterId == characterId && !c.IsRemoved)
            .Select(c => new {c.GameId})
            .FirstOrDefaultAsync();
        return gameWrapper?.GameId;
    }
}