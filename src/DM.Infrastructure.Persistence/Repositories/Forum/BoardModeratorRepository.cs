using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Boards;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class BoardModeratorRepository : IBoardModeratorRepository
{
    private readonly DmDbContext _dmDbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BoardModeratorRepository(
        DmDbContext dmDbContext,
        IMapper mapper)
    {
        _dmDbContext = dmDbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid boardId)
    {
        return await _dmDbContext.BoardModerators
            .TagWith("DM.Forum.ModeratorsList")
            .Where(m => m.BoardId == boardId)
            .Select(m => m.User)
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }
}
