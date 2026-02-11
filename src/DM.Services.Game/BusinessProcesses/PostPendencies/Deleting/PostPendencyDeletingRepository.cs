using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbPostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Deleting;

/// <inheritdoc />
internal class PostPendencyDeletingRepository : IPostPendencyDeletingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostPendencyDeletingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<PostPendency?> Get(Guid pendencyId)
    {
        return _dbContext.PostPendencies
            .Where(e => e.PendencyId == pendencyId)
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    /// <inheritdoc />
    public Task Delete(IUpdateBuilder<DbPostPendency> updateBuilder)
    {
        updateBuilder.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }
}
