using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <inheritdoc />
internal class PostPendencyCreatingRepository : IPostPendencyCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostPendencyCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PostPendency> Create(DataAccess.BusinessObjects.Games.Links.PostPendency postPendency)
    {
        _dbContext.PostPendencies.Add(postPendency);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.PostPendencies
            .Where(e => e.PendencyId == postPendency.PendencyId)
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
