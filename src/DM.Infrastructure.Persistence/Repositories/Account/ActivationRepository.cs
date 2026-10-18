using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
internal class ActivationRepository : IActivationRepository
{
    private readonly DmDbContext _dbContext;

    public ActivationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByToken(Guid secret, CancellationToken cancellationToken = default)
    {
        var hash = ConfirmationSecret.Hash(secret);
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.SecretHash == hash, cancellationToken);

        if (entity == null)
            return null;

        return new PendingRegistration
        {
            PendingRegistrationId = entity.PendingRegistrationId,
            SecretHash = entity.SecretHash,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Salt = entity.Salt,
            PasswordHashVersion = entity.PasswordHashVersion,
            CreatedUtc = entity.CreatedUtc,
            TokenCreatedUtc = entity.TokenCreatedUtc,
            AcceptedRules = entity.AcceptedRules
        };
    }

    /// <inheritdoc />
    public async Task CompleteActivation(CreateUser user, Guid pendingId)
    {
        var dbUser = new DbUser
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Salt = user.Salt,
            PasswordHashVersion = user.PasswordHashVersion,
            CreatedUtc = user.CreatedUtc,
            Role = user.Role,
            AccessPolicy = user.AccessPolicy,
            LastActivityUtc = user.CreatedUtc
        };

        // The zone of the device the account was created on, written with the
        // account so the first letter already carries it. Without one there is no
        // row to write: an account without settings reads the defaults, UTC among
        // them.
        var settings = user.TimeZoneId == null
            ? null
            : Entities.Account.Settings.UserSettings.CreateDefault(user.UserId);
        if (settings != null)
        {
            settings.TimeZoneId = user.TimeZoneId;
        }

        // Both writes or neither: an account created without its pending row
        // removed can be activated a second time, and a pending row removed
        // without its account is a registration that can never be finished and
        // cannot be started again either - the address is taken by a row nobody
        // can log in as.
        //
        // dbUser is built above the block, so a replay re-adds the same instance
        // rather than minting a second identifier, and the pending row is re-read
        // inside it from whatever the rollback restored.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Create the user
                _dbContext.Users.Add(dbUser);
                if (settings != null)
                {
                    _dbContext.UserSettings.Add(settings);
                }

                // Delete the pending registration
                var pending = await _dbContext.PendingRegistrations.FindAsync(pendingId);
                if (pending != null)
                {
                    _dbContext.PendingRegistrations.Remove(pending);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // The name or the address was free when it was checked and is not
                // free now. Which of the two it was is not read off the constraint
                // name: the caller decides by looking at what is there, because the
                // same collision is an idempotent second click for one person and a
                // taken name for another. Translated here so the domain need not
                // know the storage engine's codes.
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw new DuplicateEntityException("Duplicate account", ex);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> FindUserByEmail(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Where(u => u.Email.ToLower() == email.ToLower())
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
