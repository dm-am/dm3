using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameUserRepository : IGameUserRepository
{
    private readonly DmDbContext _dbContext;

    public GameUserRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    #region Players

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetPlayers(Guid gameId)
    {
        return await _dbContext.Characters
            .Where(c => c.GameId == gameId && !c.IsRemoved && c.Status == CharacterStatus.Active && !c.IsNpc)
            .Select(c => c.Author)
            .Distinct()
            .Select(u => new GeneralUser
            {
                UserId = u!.UserId,
                Username = u.Username,
                Role = u.Role,
                Status = u.Status
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> IsPlayer(Guid gameId, string username)
    {
        return await _dbContext.Characters
            .AnyAsync(c => c.GameId == gameId &&
                          c.Author!.Username.ToLower() == username.ToLower() &&
                          !c.IsRemoved &&
                          c.Status == CharacterStatus.Active &&
                          !c.IsNpc);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> ExilePlayer(Guid gameId, string username)
    {
        var usernameLower = username.ToLower();
        var characterIds = await _dbContext.Characters
            .Where(c => c.GameId == gameId &&
                       c.Author!.Username.ToLower() == usernameLower &&
                       !c.IsRemoved &&
                       c.Status == CharacterStatus.Active)
            .Select(c => c.CharacterId)
            .ToListAsync();

        if (characterIds.Count > 0)
        {
            await _dbContext.Characters
                .Where(c => characterIds.Contains(c.CharacterId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.Status, CharacterStatus.Retired)
                    .SetProperty(c => c.IsPlayerExiled, true));
        }

        return characterIds;
    }

    #endregion

    #region Assistants

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
                Status = u.Status
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

    #endregion
}
