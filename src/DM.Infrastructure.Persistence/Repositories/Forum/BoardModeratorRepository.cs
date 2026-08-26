using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Boards;
using DM.Infrastructure.Persistence.Entities.Forum;
using Microsoft.EntityFrameworkCore;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class BoardModeratorRepository : IBoardModeratorRepository
{
    private readonly DmDbContext _dmDbContext;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public BoardModeratorRepository(
        DmDbContext dmDbContext,
        IGuidFactory guidFactory)
    {
        _dmDbContext = dmDbContext;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid boardId)
    {
        return await _dmDbContext.BoardModerators
            .TagWith("DM.Forum.ModeratorsList")
            .Where(m => m.BoardId == boardId)
            .Select(m => m.User)
            .ProjectToGeneralUser()
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task Add(Guid boardId, Guid userId)
    {
        var boardModerator = new BoardModerator
        {
            BoardModeratorId = _guidFactory.Create(),
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
