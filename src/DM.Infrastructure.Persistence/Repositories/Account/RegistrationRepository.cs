using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

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
    public Task AddPending(PendingRegistration pending) =>
        // One statement: two submissions arriving together would both pass a check-then-insert.
        // CreatedUtc stays on update: the sweep of abandoned addresses counts from it.
        _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PendingRegistrations" (
                "PendingRegistrationId", "SecretHash", "Email", "PasswordHash", "Salt",
                "PasswordHashVersion", "CreatedUtc", "TokenCreatedUtc", "AcceptedRules")
            VALUES (
                {pending.PendingRegistrationId}, {pending.SecretHash}, {pending.Email},
                {pending.PasswordHash}, {pending.Salt}, {pending.PasswordHashVersion},
                {pending.CreatedUtc}, {pending.TokenCreatedUtc}, {pending.AcceptedRules})
            ON CONFLICT ("Email") DO UPDATE SET
                "SecretHash" = {pending.SecretHash},
                "PasswordHash" = {pending.PasswordHash},
                "Salt" = {pending.Salt},
                "PasswordHashVersion" = {pending.PasswordHashVersion},
                "TokenCreatedUtc" = {pending.TokenCreatedUtc},
                "AcceptedRules" = {pending.AcceptedRules}
            """);

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByEmail(string email, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower(), cancellationToken);
        return entity?.ToPendingRegistration();
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
