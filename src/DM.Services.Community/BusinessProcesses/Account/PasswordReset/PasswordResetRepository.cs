using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <inheritdoc />
internal class PasswordResetRepository : IPasswordResetRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PasswordResetRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task ReplacePasswordResetToken(Guid userId, Token token)
    {
        // Invalidate old password reset tokens
        var oldTokens = await _dbContext.Tokens
            .Where(t => t.UserId == userId && t.Type == TokenType.PasswordChange)
            .ToListAsync();

        foreach (var oldToken in oldTokens)
        {
            oldToken.IsRemoved = true;
        }

        // Add new token
        _dbContext.Tokens.Add(token);
        await _dbContext.SaveChangesAsync();
    }
}