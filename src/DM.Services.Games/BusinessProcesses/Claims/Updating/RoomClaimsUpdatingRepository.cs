using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoomClaim = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomClaim;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Updating;

/// <inheritdoc />
internal class RoomClaimsUpdatingRepository : IRoomClaimsUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomClaimsUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<RoomClaim> Update(IUpdateBuilder<DbRoomClaim> updateClaim)
    {
        var linkId = updateClaim.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.RoomClaims
            .Where(l => l.RoomClaimId == linkId)
            .ProjectTo<RoomClaim>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}