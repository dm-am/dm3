using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Tokens;
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
        // By the hash, the only half of the link the table holds. No age: a link is
        // spent whenever it is redeemed.
        var hash = ConfirmationSecret.Hash(secret);
        return _dbContext.Tokens
            .Where(t => t.SecretHash == hash && t.Type == TokenType.EmailChange && !t.IsRemoved)
            .Retire();
    }
}
