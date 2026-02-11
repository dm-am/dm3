using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;

/// <inheritdoc />
internal class EmailChangeConfirmationRepository : IEmailChangeConfirmationRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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
