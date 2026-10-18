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
    /// Type, removal and age are checked here rather than left to the caller: the
    /// invariant used to hold only because a validator happened to run first, and
    /// one path — reading token info — called this with no validator at all.
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
    /// The same query the redemption runs, and not a cheaper one without the
    /// user: the cheaper one answered "ready" for a token whose account had been
    /// removed since, and the form it opened then failed on the write. What the
    /// page is told on open is what the write will find.
    /// </remarks>
    public async Task<bool> TokenValid(Guid secret, DateTimeOffset createdSince) =>
        await FindUser(secret, createdSince) != null;

    /// <inheritdoc />
    public async Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate)
    {
        // Both writes or neither. Separately, a refusal between them left the new
        // password in place with the link that authorised it still live: whoever
        // holds that mail can set the password again, and nothing here notices.
        //
        // The user row is written first and the tokens after it, and the issue of
        // a reset link takes the same two rows in the same order: that is what
        // makes a change of password and a request for a new link queue up rather
        // than interleave.
        //
        // Both writes are set-based, so the change tracker takes no part and a
        // replay of this block needs nothing cleared.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _dbContext.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PasswordHash, passwordHash)
                    .SetProperty(u => u.Salt, salt)
                    // The version travels with the hash. Leaving it behind produces a row
                    // whose stored version no longer describes its hash - harmless while
                    // there is only one scheme, and unrecoverable the moment there are two.
                    .SetProperty(u => u.PasswordHashVersion, PasswordHashing.CurrentVersion)
                    // A pending change of address goes with the password. The change
                    // is confirmed from the new mailbox by whoever asked for it, and a
                    // new password ends that person's sessions but not their mailbox:
                    // left in place, the address moved to them after the owner had
                    // already locked them out.
                    .SetProperty(u => u.PendingEmail, (string?)null));

            // Every live reset link of the account, not only the one this change
            // came in through. A link requested while the change was in flight
            // survived the change and set the password again afterwards; and a
            // change made from inside a session used to leave older links alive
            // altogether, which is the same hole reached by the other door.
            // The confirmation link of the dropped address goes with it: a link
            // that answers "nothing to apply" and a spent one read the same to
            // the person holding it, and only the spent one reads the same in
            // the table.
            await _dbContext.Tokens
                .Where(t => t.UserId == userId &&
                            (t.Type == TokenType.PasswordChange || t.Type == TokenType.EmailChange) &&
                            !t.IsRemoved)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));

            await transaction.CommitAsync();
        });
    }
}
