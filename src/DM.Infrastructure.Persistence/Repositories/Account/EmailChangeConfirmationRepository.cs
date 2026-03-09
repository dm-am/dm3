using System;
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
    public async Task<Guid?> FindEmailChangeToken(Guid tokenId, DateTimeOffset createdSince)
    {
        return (await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId &&
                        t.Type == TokenType.EmailChange &&
                        !t.IsRemoved &&
                        t.CreatedUtc > createdSince)
            .Select(t => new { t.TokenId })
            .FirstOrDefaultAsync())?.TokenId;
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
