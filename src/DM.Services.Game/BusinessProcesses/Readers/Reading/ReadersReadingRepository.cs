using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Readers.Reading;

/// <inheritdoc />
internal class ReadersReadingRepository : IReadersReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReadersReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid gameId, Guid userId)
    {
        return await _dbContext.Readers
            .Where(r => r.GameId == gameId)
            .Select(g => g.User)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }
}