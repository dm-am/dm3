using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using Microsoft.EntityFrameworkCore;
using TokenEntity = DM.Infrastructure.Persistence.Entities.Account.Token;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class PasswordResetRepository : IPasswordResetRepository
{
    private readonly DmDbContext _dbContext;

    public PasswordResetRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task ReplacePasswordResetToken(Guid userId, CreateToken tokenDto)
    {
        // Invalidate old password reset tokens
        await _dbContext.Tokens
            .Where(t => t.UserId == userId && t.Type == TokenType.PasswordChange && !t.IsRemoved)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));

        // Add new token
        var tokenEntity = new TokenEntity
        {
            TokenId = tokenDto.TokenId,
            UserId = tokenDto.UserId,
            EntityId = tokenDto.EntityId,
            CreatedUtc = tokenDto.CreatedUtc,
            Type = tokenDto.Type,
            CreatorId = tokenDto.CreatorId,
            IsRemoved = false
        };
        _dbContext.Tokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync();
    }
}
