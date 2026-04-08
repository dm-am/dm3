using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Boards;
using DM.Infrastructure.Persistence.Entities.Forum;
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

    /// <inheritdoc />
    public async Task Add(Guid boardId, Guid userId)
    {
        var boardModerator = new BoardModerator
        {
            BoardModeratorId = Guid.NewGuid(),
            BoardId = boardId,
            UserId = userId
        };
        await _dmDbContext.BoardModerators.AddAsync(boardModerator);
        await _dmDbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Remove(Guid boardId, Guid userId)
    {
        var moderator = await _dmDbContext.BoardModerators
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (moderator != null)
        {
            _dmDbContext.BoardModerators.Remove(moderator);
            await _dmDbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsModerator(Guid boardId, Guid userId)
    {
        return await _dmDbContext.BoardModerators
            .AnyAsync(m => m.BoardId == boardId && m.UserId == userId);
    }
}
