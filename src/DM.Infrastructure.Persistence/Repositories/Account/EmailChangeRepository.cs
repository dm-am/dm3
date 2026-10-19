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

            // The account row is claimed first, so a second request from another tab
            // cannot leave two live letters.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "UserId" FROM "Users" WHERE "UserId" = {0} FOR UPDATE""", userId);

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return;
            }

            await _dbContext.Tokens
                .Where(t => t.UserId == userId && t.Type == TokenType.EmailChange && !t.IsRemoved)
                .Retire();

            // Pending, not current: the address changes once the link in the letter is followed.
            user.PendingEmail = newEmail;
            _dbContext.Tokens.Add(tokenEntity);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// The move or the giving up on it, in one transaction: EF rolls a refused
    /// SaveChanges back to its savepoint, so the compensating write still commits.
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
                return;
            }

            // Both halves in one save, so the change is neither offered twice nor lost.
            var move = new EmailMove(user.Email, user.PendingEmail);
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsDuplicateKey())
            {
                // Somebody took the address while the letter sat in the mailbox.
                _dbContext.ChangeTracker.Clear();

                // The request is over, so its pending address goes too; set-based,
                // because the tracker was just cleared.
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
