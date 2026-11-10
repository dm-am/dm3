using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

using DM.Infrastructure.Persistence.Shared.Users;
using DM.Infrastructure.Persistence.Shared.Tokens;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class PasswordChangeRepository : IPasswordChangeRepository
{
    private readonly DmDbContext _dbContext;

    public PasswordChangeRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(string username) =>
        _dbContext.Users.FindAuthenticatedUser(username);

    /// <inheritdoc />
    /// <remarks>
    /// Type, removal and age are checked here, not left to a validator a caller may skip.
    /// </remarks>
    public Task<AuthenticatedUser?> FindUser(Guid secret, DateTimeOffset createdSince)
    {
        var hash = ConfirmationSecret.Hash(secret);
        return _dbContext.Tokens
            .Where(t => t.SecretHash == hash &&
                        t.Type == TokenType.PasswordChange &&
                        !t.IsRemoved &&
                        t.CreatedUtc > createdSince)
            .Select(t => t.User)
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The query the redemption runs, so what the page is told on open is what the write finds.
    /// </remarks>
    public async Task<bool> TokenValid(Guid secret, DateTimeOffset createdSince) =>
        await FindUser(secret, createdSince) != null;

    /// <inheritdoc />
    /// <remarks>
    /// The account filter leaves a removed account out of the update, so an account
    /// removed after the lookup counts zero rows and its links stay as they were.
    /// </remarks>
    public async Task<bool> UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate)
    {
        var updated = false;

        // Both writes or neither, user row first and tokens after, in the order the
        // issue of a reset link takes them, so the two queue up rather than interleave.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var rows = await _dbContext.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PasswordHash, passwordHash)
                    .SetProperty(u => u.Salt, salt)
                    // The version travels with the hash.
                    .SetProperty(u => u.PasswordHashVersion, PasswordHashing.CurrentVersion)
                    // A pending change of address goes with the password: a new password
                    // ends the asker's sessions but not their mailbox.
                    .SetProperty(u => u.PendingEmail, (string?)null));
            updated = rows > 0;
            if (!updated)
            {
                return;
            }

            // Every live reset link of the account, not only the one this change came
            // in through, and the confirmation link of the dropped address.
            await _dbContext.Tokens
                .Where(t => t.UserId == userId &&
                            (t.Type == TokenType.PasswordChange || t.Type == TokenType.EmailChange) &&
                            !t.IsRemoved)
                .Retire();

            await transaction.CommitAsync();
        });

        return updated;
    }
}
