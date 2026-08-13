using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbPendingRegistration = DM.Infrastructure.Persistence.Entities.Account.PendingRegistration;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// Registration repository for email-first registration flow
/// </summary>
internal class RegistrationRepository : IRegistrationRepository
{
    private readonly DmDbContext _dbContext;

    public RegistrationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> EmailFreeForNewRegistration(string email, CancellationToken cancellationToken)
    {
        // Email must not be held by any account, deactivated ones included
        if (await AccountReservation.EmailTaken(_dbContext.Users, email, cancellationToken))
            return false;

        // Email in PendingRegistrations is OK - we'll replace it
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> UsernameFree(string username, CancellationToken cancellationToken) =>
        !await AccountReservation.UsernameTaken(_dbContext.Users, username, ct: cancellationToken) &&
        !await _dbContext.UsernameHistories.AnyAsync(h => h.OldUsername.ToLower() == username.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> PendingExists(string email, CancellationToken cancellationToken) =>
        await _dbContext.PendingRegistrations
            .AnyAsync(p => p.Email.ToLower() == email.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task AddPending(PendingRegistration pending)
    {
        var entity = new DbPendingRegistration
        {
            PendingRegistrationId = pending.PendingRegistrationId,
            SecretHash = pending.SecretHash,
            Email = pending.Email,
            PasswordHash = pending.PasswordHash,
            Salt = pending.Salt,
            PasswordHashVersion = pending.PasswordHashVersion,
            CreatedUtc = pending.CreatedUtc,
            TokenCreatedUtc = pending.TokenCreatedUtc,
            AcceptedRules = pending.AcceptedRules
        };
        _dbContext.PendingRegistrations.Add(entity);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ReplacePending(PendingRegistration pending)
    {
        // Equality over lower(), not ILIKE. The row found here is overwritten with
        // a new token and a new password hash and keeps its own address, while the
        // confirmation letter goes to the address the caller typed: a pattern match
        // would hand the caller a link that activates somebody else's registration.
        var existing = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email.ToLower() == pending.Email.ToLower());

        if (existing != null)
        {
            // Update existing pending with new data
            existing.SecretHash = pending.SecretHash;
            existing.PasswordHash = pending.PasswordHash;
            existing.Salt = pending.Salt;
            existing.PasswordHashVersion = pending.PasswordHashVersion;
            existing.TokenCreatedUtc = pending.TokenCreatedUtc;
            existing.AcceptedRules = pending.AcceptedRules;
            // Keep original CreatedUtc for cleanup logic
        }
        else
        {
            // Should not happen, but add if somehow missing
            var entity = new DbPendingRegistration
            {
                PendingRegistrationId = pending.PendingRegistrationId,
                SecretHash = pending.SecretHash,
                Email = pending.Email,
                PasswordHash = pending.PasswordHash,
                Salt = pending.Salt,
                PasswordHashVersion = pending.PasswordHashVersion,
                CreatedUtc = pending.CreatedUtc,
                TokenCreatedUtc = pending.TokenCreatedUtc,
                AcceptedRules = pending.AcceptedRules
            };
            _dbContext.PendingRegistrations.Add(entity);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByEmail(string email, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower(), cancellationToken);

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
    public async Task UpdatePending(PendingRegistration pending)
    {
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == pending.PendingRegistrationId);

        if (entity != null)
        {
            entity.SecretHash = pending.SecretHash;
            entity.Email = pending.Email;
            entity.PasswordHash = pending.PasswordHash;
            entity.Salt = pending.Salt;
            entity.PasswordHashVersion = pending.PasswordHashVersion;
            entity.TokenCreatedUtc = pending.TokenCreatedUtc;
            entity.AcceptedRules = pending.AcceptedRules;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A set-based delete rather than a load-and-remove: nothing references a
    /// pending registration, so there is nothing to cascade and no reason to
    /// bring the rows into the change tracker.
    /// </remarks>
    public Task<int> DeletePendingStartedBefore(
        DateTimeOffset startedBefore, CancellationToken cancellationToken = default) =>
        _dbContext.PendingRegistrations
            .TagWith("DM.Account.PendingRegistrationRetention")
            .Where(p => p.CreatedUtc < startedBefore)
            .ExecuteDeleteAsync(cancellationToken);
}
