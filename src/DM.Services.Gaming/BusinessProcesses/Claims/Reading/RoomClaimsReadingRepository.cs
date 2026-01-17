using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Gaming.BusinessProcesses.Shared;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Reading;

/// <inheritdoc />
internal class RoomClaimsReadingRepository : IRoomClaimsReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RoomClaimsReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoomClaim>> GetGameClaims(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.GameId == gameId)
            .SelectMany(r => r.RoomClaims)
            .ProjectTo<RoomClaim>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoomClaim>> GetRoomClaims(Guid roomId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(r => r.RoomId == roomId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Select(r => r.RoomClaims)
            .ProjectTo<RoomClaim>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<RoomClaim> GetClaim(Guid claimId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.RoomClaims)
            .Where(l => l.RoomClaimId == claimId)
            .ProjectTo<RoomClaim>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}