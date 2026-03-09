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

    /// <inheritdoc />
    public async Task<int> MarkCharactersAsLeft(Guid userId, Guid gameId)
    {
        var characters = await _dbContext.Characters
            .Where(c => c.AuthorId == userId && c.GameId == gameId &&
                        !c.IsRemoved && c.Status == CharacterStatus.Active)
            .ToListAsync();

        foreach (var character in characters)
        {
            character.Status = CharacterStatus.Retired;
            character.IsPlayerLeft = true;
        }

        await _dbContext.SaveChangesAsync();
        return characters.Count;
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
    public async Task<bool> IsAssistantByUserId(Guid userId, Guid gameId)
    {
        return await _dbContext.GameAssistants
            .AnyAsync(a => a.UserId == userId && a.GameId == gameId);
    }

    /// <inheritdoc />
    public async Task RemoveAssistantByUsername(Guid gameId, string username)
    {
        var usernameLower = username.ToLower();
        await _dbContext.GameAssistants
            .Where(ga => ga.GameId == gameId && ga.User.Username.ToLower() == usernameLower)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task RemoveAssistantByUserId(Guid userId, Guid gameId)
    {
        var assistant = await _dbContext.GameAssistants
            .FirstOrDefaultAsync(a => a.UserId == userId && a.GameId == gameId);
        if (assistant != null)
        {
            _dbContext.GameAssistants.Remove(assistant);
            await _dbContext.SaveChangesAsync();
        }
    }

    #endregion

    #region Readers

    /// <inheritdoc />
    public async Task<bool> IsReader(Guid userId, Guid gameId)
    {
        return await _dbContext.Subscriptions
            .AnyAsync(s => s.SubscriberId == userId &&
                          s.TargetType == SubscriptionTargetType.Game &&
                          s.TargetId == gameId);
    }

    /// <inheritdoc />
    public async Task RemoveReader(Guid userId, Guid gameId)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == userId &&
                                      s.TargetType == SubscriptionTargetType.Game &&
                                      s.TargetId == gameId);
        if (subscription != null)
        {
            _dbContext.Subscriptions.Remove(subscription);
            await _dbContext.SaveChangesAsync();
        }
    }

    #endregion
}
