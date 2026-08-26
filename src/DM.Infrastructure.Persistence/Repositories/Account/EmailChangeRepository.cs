using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Infrastructure.Persistence.Shared.Tokens;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class EmailChangeRepository : IEmailChangeRepository
{
    private readonly DmDbContext _dbContext;

    public EmailChangeRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(string username) =>
        _dbContext.Users.FindAuthenticatedUser(username);

    /// <inheritdoc />
    public async Task<bool> IsEmailFree(string email, CancellationToken ct) =>
        !await AccountReservation.EmailTaken(_dbContext.Users, email, ct);

    /// <inheritdoc />
    public async Task RequestChange(Guid userId, string newEmail, CreateToken tokenDto)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return;

        // Pending, not current. The account keeps answering at the old address
        // until the link in the letter is followed: writing the new one here made
        // the confirmation a formality with nothing left to confirm, and a typo
        // took the account away from its owner along with any way to be told.
        user.PendingEmail = newEmail;

        var tokenEntity = TokenRows.From(tokenDto);
        _dbContext.Tokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> ApplyPendingEmail(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user?.PendingEmail is null)
        {
            return false;
        }

        // Both halves in one save: an account whose address moved but whose
        // pending value stayed would offer the same change again, and one whose
        // pending value cleared without the address moving would lose the request
        // with the letter already sent.
        user.Email = user.PendingEmail;
        user.PendingEmail = null;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task InvalidateOldEmailChangeTokens(Guid userId)
    {
        var oldTokens = await _dbContext.Tokens
            .Where(t => t.UserId == userId && t.Type == TokenType.EmailChange && !t.IsRemoved)
            .ToListAsync();

        foreach (var token in oldTokens)
        {
            token.IsRemoved = true;
        }

        if (oldTokens.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
