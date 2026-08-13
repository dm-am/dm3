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
    public async Task MarkTokenUsed(Guid tokenId)
    {
        var token = await _dbContext.Tokens.FindAsync(tokenId);
        if (token != null)
        {
            token.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
