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
        return entity?.ToPendingRegistration();
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

        // Both writes or neither, or the registration can be activated twice or never.
        // dbUser is built above the block, so a replay re-adds the same instance.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                _dbContext.Users.Add(dbUser);
                if (settings != null)
                {
                    _dbContext.UserSettings.Add(settings);
                }

                var pending = await _dbContext.PendingRegistrations.FindAsync(pendingId);
                if (pending != null)
                {
                    _dbContext.PendingRegistrations.Remove(pending);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (ex.IsDuplicateKey())
            {
                // The name or the address was taken after the check; the caller tells a
                // second click from a taken name by reading what is there.
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw new DuplicateEntityException("Duplicate account", ex);
            }
        });
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> FindUserByEmail(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Where(AccountNames.EmailIs(email))
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
