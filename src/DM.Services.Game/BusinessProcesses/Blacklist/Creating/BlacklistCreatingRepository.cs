using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Creating;

/// <inheritdoc />
internal class BlacklistCreatingRepository : IBlacklistCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlacklistCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Create(GameBlacklist link)
    {
        _dbContext.GameBlacklists.Add(link);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.GameBlacklists
            .Where(l => l.EntryId == link.EntryId)
            .Select(l => l.User)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}