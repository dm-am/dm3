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
        // Built outside the block: a retry replays the block, and the identifier
        // and the hash of the token in the letter must not change under it.
        var tokenEntity = new TokenEntity
        {
            TokenId = tokenDto.TokenId,
            // The letter carries tokenDto.Secret; the row keeps only its hash.
            SecretHash = tokenDto.SecretHash,
            UserId = tokenDto.UserId,
            EntityId = tokenDto.EntityId,
            CreatedUtc = tokenDto.CreatedUtc,
            Type = tokenDto.Type,
            CreatorId = tokenDto.CreatorId,
            IsRemoved = false
        };
        // Both writes or neither. Separately, a refusal between them left the
        // account with every reset link dead and no new one issued, on the one path
        // a person reaches when they cannot log in.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure and
        // a retrying strategy refuses a transaction opened by hand.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempted = false;
        await strategy.ExecuteAsync(async () =>
        {
            if (attempted)
            {
                // A retry replays the whole block, so what the failed attempt left
                // tracked has to go before the same entity is added again.
                _dbContext.ChangeTracker.Clear();
            }

            attempted = true;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _dbContext.Tokens
                .Where(t => t.UserId == userId && t.Type == TokenType.PasswordChange && !t.IsRemoved)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));

            _dbContext.Tokens.Add(tokenEntity);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }
}
