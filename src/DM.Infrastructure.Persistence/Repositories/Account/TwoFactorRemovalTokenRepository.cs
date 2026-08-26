using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Shared.Tokens;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class TwoFactorRemovalTokenRepository : ITwoFactorRemovalTokenRepository
{
    private readonly DmDbContext _dbContext;

    public TwoFactorRemovalTokenRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task ReplaceToken(
        Guid userId, CreateToken tokenDto, CancellationToken cancellationToken = default)
    {
        // Built outside the block: a retry replays it, and the identifier and
        // the hash of the value already in the letter must not change under it.
        var tokenEntity = TokenRows.From(tokenDto);

        // Retiring the previous link and issuing the new one are one act: a
        // refusal between them leaves the account with the older letter dead and
        // no newer one, on a path somebody reaches when they cannot sign in.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.Tokens
                .Where(t => t.UserId == userId && t.Type == tokenDto.Type && !t.IsRemoved)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true), cancellationToken);

            _dbContext.Tokens.Add(tokenEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<Guid?> RedeemToken(
        Guid secret, TokenType type, DateTimeOffset? liveSince,
        CancellationToken cancellationToken = default)
    {
        // Looked up by the hash, because the row never carried the value the
        // letter did.
        var hash = ConfirmationSecret.Hash(secret);

        var candidate = await _dbContext.Tokens
            .Where(t => t.Type == type && !t.IsRemoved && t.SecretHash != null && t.SecretHash == hash)
            .Where(t => liveSince == null || t.CreatedUtc >= liveSince)
            .Select(t => new { t.TokenId, t.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate == null)
        {
            return null;
        }

        // Conditional on the link still being live: a letter opened twice, or
        // opened by a mail scanner and then by its reader, spends once.
        var spent = await _dbContext.Tokens
            .Where(t => t.TokenId == candidate.TokenId && !t.IsRemoved)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true), cancellationToken);

        return spent == 1 ? candidate.UserId : null;
    }
}
