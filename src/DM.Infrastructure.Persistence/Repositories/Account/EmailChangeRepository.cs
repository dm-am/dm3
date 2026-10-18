using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Infrastructure.Persistence.Shared.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        // Built outside the block: a replay runs the block again, and the hash of
        // the secret already written into the letter must not change under it.
        var tokenEntity = TokenRows.From(tokenDto);

        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // The account row is claimed first, and the retirement of the old
            // links belongs inside this same transaction. Written as two calls it
            // interleaved with a second request from another tab: both retired
            // what they found and both wrote a pending address, leaving two live
            // letters of which the older one pointed at an address the owner had
            // already replaced.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "UserId" FROM "Users" WHERE "UserId" = {0} FOR UPDATE""", userId);

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                await transaction.RollbackAsync();
                return;
            }

            await _dbContext.Tokens
                .Where(t => t.UserId == userId && t.Type == TokenType.EmailChange && !t.IsRemoved)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));

            // Pending, not current. The account keeps answering at the old address
            // until the link in the letter is followed: writing the new one here made
            // the confirmation a formality with nothing left to confirm, and a typo
            // took the account away from its owner along with any way to be told.
            user.PendingEmail = newEmail;
            _dbContext.Tokens.Add(tokenEntity);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// Two writes, of which exactly one happens: the move, or the giving up on
    /// it. They share a transaction all the same, because the second is the
    /// undoing of the first and has to outlive the refusal that called for it.
    /// EF takes a savepoint before a SaveChanges inside a transaction somebody
    /// else opened and rolls back to it when the statement is refused, so the
    /// transaction is still usable afterwards and the compensating write commits
    /// on its own.
    /// </remarks>
    public async Task<EmailMove?> ApplyPendingEmail(Guid userId)
    {
        EmailMove? applied = null;

        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var user = await _dbContext.Users.FindAsync(userId);
            if (user?.PendingEmail is null)
            {
                await transaction.RollbackAsync();
                return;
            }

            // Both halves in one save: an account whose address moved but whose
            // pending value stayed would offer the same change again, and one whose
            // pending value cleared without the address moving would lose the request
            // with the letter already sent.
            var move = new EmailMove(user.Email, user.PendingEmail);
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // The address was free when the change was asked for and is not free
                // now: somebody else registered or moved onto it while the letter sat
                // in the mailbox. The unique index over lower("Email") is what holds
                // the rule, and it is translated here so the caller answers the same
                // "already taken" the form answers, rather than a failure of the
                // storage engine.
                _dbContext.ChangeTracker.Clear();

                // The request is over, so the address it was waiting for goes with
                // it. Left standing, the pending value keeps the account offering a
                // change that can never go through: the profile advertises an
                // address it will never get, and every later click on the same
                // letter fails the same way. Set-based rather than through the
                // tracker, which has just been cleared of the save the index
                // refused.
                await _dbContext.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.PendingEmail, (string?)null));

                await transaction.CommitAsync();
                throw new DuplicateEntityException("Duplicate account email", ex);
            }

            applied = move;
            await transaction.CommitAsync();
        });

        return applied;
    }
}
