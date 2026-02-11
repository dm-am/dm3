using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbRoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Updating;

/// <inheritdoc />
internal class RoomAccessUpdatingRepository : IRoomAccessUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomAccessUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<RoomAccess> Update(IUpdateBuilder<DbRoomAccess> updateAccess)
    {
        var linkId = updateAccess.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.RoomAccesses
            .Where(l => l.AccessId == linkId)
            .ProjectTo<RoomAccess>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}