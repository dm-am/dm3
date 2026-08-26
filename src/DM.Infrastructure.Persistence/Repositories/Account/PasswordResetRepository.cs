using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Shared.Tokens;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

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
        var tokenEntity = TokenRows.From(tokenDto);
        // Both writes or neither. Separately, a refusal between them left the
        // account with every reset link dead and no new one issued, on the one path
        // a person reaches when they cannot log in.
        await RetryableWrite.Run(_dbContext, async () =>
        {
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
