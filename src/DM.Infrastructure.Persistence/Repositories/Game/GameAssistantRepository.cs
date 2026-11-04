using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameAssistantRepository : IGameAssistantRepository
{
    private readonly DmDbContext _dbContext;

    public GameAssistantRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetAssistants(Guid gameId)
    {
        return await _dbContext.GameAssistants
            .Where(ga => ga.GameId == gameId)
            .Select(ga => ga.User)
            .Select(u => new GeneralUser
            {
                UserId = u.UserId,
                Username = u.Username,
                Role = u.Role,
                Status = u.Status,
                LastActivityUtc = u.LastActivityUtc,
                // The newbie mark of the roster line is read from it.
                QuantityRating = u.QuantityRating
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> IsAssistantByUsername(Guid gameId, string username)
    {
        return await _dbContext.GameAssistants
            .AnyAsync(ga => ga.GameId == gameId && ga.User.Username.ToLower() == username.ToLower());
    }

    /// <inheritdoc />
    public async Task RemoveAssistantByUsername(Guid gameId, string username)
    {
        var usernameLower = username.ToLower();
        await _dbContext.GameAssistants
            .Where(ga => ga.GameId == gameId && ga.User.Username.ToLower() == usernameLower)
            .ExecuteDeleteAsync();
    }
}
