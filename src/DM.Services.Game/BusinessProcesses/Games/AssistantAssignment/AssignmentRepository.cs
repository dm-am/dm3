using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.AssistantAssignment;

/// <inheritdoc />
internal class AssignmentRepository : IAssignmentRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public AssignmentRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }
        
    /// <inheritdoc />
    public async Task<Guid?> FindGameToAssign(Guid tokenId, Guid userId)
    {
        var gameIdWrapper = await _dbContext.Tokens
            .Where(t => !t.IsRemoved &&
                        t.TokenId == tokenId &&
                        t.UserId == userId &&
                        t.Type == TokenType.AssistantAssignment)
            .Select(t => new {t.EntityId})
            .FirstOrDefaultAsync();
        return gameIdWrapper?.EntityId;
    }

    /// <inheritdoc />
    public Task AssignAssistant(IUpdateBuilder<DbGame> updateGame, IUpdateBuilder<Token> updateToken)
    {
        updateGame.AttachTo(_dbContext);
        updateToken.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> FindAssignments(Guid gameId)
    {
        return await _dbContext.Tokens
            .Where(t => !t.IsRemoved &&
                        t.EntityId == gameId &&
                        t.Type == TokenType.AssistantAssignment)
            .Select(t => t.TokenId)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task InvalidateAndCreate(IEnumerable<IUpdateBuilder<Token>> updates, Token token)
    {
        foreach (var updateBuilder in updates)
        {
            updateBuilder.AttachTo(_dbContext);
        }

        _dbContext.Tokens.Add(token);
        return _dbContext.SaveChangesAsync();
    }
}