using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class EmailChangeConfirmationRepository : IEmailChangeConfirmationRepository
{
    private readonly DmDbContext _dbContext;

    public EmailChangeConfirmationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindEmailChangeTokenOwner(Guid secret, DateTimeOffset createdSince)
    {
        var hash = ConfirmationSecret.Hash(secret);
        return (await _dbContext.Tokens
            .Where(t => t.SecretHash == hash &&
                        t.Type == TokenType.EmailChange &&
                        !t.IsRemoved &&
                        t.CreatedUtc > createdSince)
            .Select(t => new { t.UserId })
            .FirstOrDefaultAsync())?.UserId;
    }

    /// <inheritdoc />
    public Task MarkTokenUsed(Guid secret)
    {
        // By the hash, because that is the only half of the link the table holds.
        // Searched by row identifier this matched nothing whatever was handed in -
        // the letter carries the secret, and the identifier is a second value the
        // caller never sees - so the write ran over an empty set and the link the
        // confirmation had just spent stayed live.
        //
        // No age in the predicate: a link is spent whenever it is redeemed, and
        // whether it was still within its window is the lookup's question.
        var hash = ConfirmationSecret.Hash(secret);
        return _dbContext.Tokens
            .Where(t => t.SecretHash == hash && t.Type == TokenType.EmailChange && !t.IsRemoved)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));
    }
}
