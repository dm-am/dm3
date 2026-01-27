using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Gaming.BusinessProcesses.Games.Invitations;

/// <inheritdoc />
internal class InvitationRepository : IInvitationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;

    /// <inheritdoc />
    public InvitationRepository(
        DmDbContext dbContext,
        IUpdateBuilderFactory updateBuilderFactory)
    {
        _dbContext = dbContext;
        _updateBuilderFactory = updateBuilderFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> FindInvitations(Guid gameId, TokenType type)
    {
        return await _dbContext.Tokens
            .Where(t => t.EntityId == gameId && t.Type == type && !t.IsRemoved)
            .Select(t => t.TokenId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> FindInvitations(Guid gameId, Guid userId, TokenType type)
    {
        return await _dbContext.Tokens
            .Where(t => t.EntityId == gameId && t.UserId == userId && t.Type == type && !t.IsRemoved)
            .Select(t => t.TokenId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> FindGameByToken(Guid tokenId, Guid userId, TokenType type)
    {
        return await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId && t.UserId == userId && t.Type == type && !t.IsRemoved)
            .Select(t => t.EntityId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task InvalidateAndCreate(IEnumerable<IUpdateBuilder<Token>> updates, Token token)
    {
        foreach (var update in updates)
        {
            update.AttachTo(_dbContext);
        }
        _dbContext.Tokens.Add(token);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Update(IUpdateBuilder<Token> update)
    {
        update.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<InvitationInfo> GetInvitation(Guid tokenId)
    {
        return await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId && !t.IsRemoved)
            .Select(t => new InvitationInfo
            {
                TokenId = t.TokenId,
                GameId = t.EntityId ?? Guid.Empty,
                GameTitle = t.Game.Title,
                UserId = t.UserId,
                UserLogin = t.User.Login,
                InviterLogin = t.Game.Master.Login,
                Type = t.Type,
                CreatedUtc = t.CreatedUtc
            })
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InvitationInfo>> GetPendingInvitations(Guid gameId)
    {
        return await _dbContext.Tokens
            .Where(t => t.EntityId == gameId && !t.IsRemoved &&
                (t.Type == TokenType.PlayerInvitation || t.Type == TokenType.ReaderInvitation))
            .Select(t => new InvitationInfo
            {
                TokenId = t.TokenId,
                GameId = t.EntityId ?? Guid.Empty,
                GameTitle = t.Game.Title,
                UserId = t.UserId,
                UserLogin = t.User.Login,
                InviterLogin = t.Game.Master.Login,
                Type = t.Type,
                CreatedUtc = t.CreatedUtc
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InvitationInfo>> GetUserPendingInvitations(Guid userId)
    {
        return await _dbContext.Tokens
            .Where(t => t.UserId == userId && !t.IsRemoved &&
                (t.Type == TokenType.AssistantAssignment ||
                 t.Type == TokenType.PlayerInvitation ||
                 t.Type == TokenType.ReaderInvitation))
            .Select(t => new InvitationInfo
            {
                TokenId = t.TokenId,
                GameId = t.EntityId ?? Guid.Empty,
                GameTitle = t.Game.Title,
                UserId = t.UserId,
                UserLogin = t.User.Login,
                InviterLogin = t.Game.Master.Login,
                Type = t.Type,
                CreatedUtc = t.CreatedUtc
            })
            .ToListAsync();
    }
}
