using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
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
        // Email must not exist in Users table
        var existsInUsers = await _dbContext.Users
            .AnyAsync(u => EF.Functions.ILike(u.Email, email), cancellationToken);
        if (existsInUsers)
            return false;

        // Email in PendingRegistrations is OK - we'll replace it
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> UsernameFree(string username, CancellationToken cancellationToken) =>
        !await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken) &&
        !await _dbContext.UsernameHistories.AnyAsync(h => h.OldUsername.ToLower() == username.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> PendingExists(string email, CancellationToken cancellationToken) =>
        await _dbContext.PendingRegistrations
            .AnyAsync(p => EF.Functions.ILike(p.Email, email), cancellationToken);

    /// <inheritdoc />
    public async Task AddPending(PendingRegistration pending)
    {
        var entity = new DbPendingRegistration
        {
            PendingRegistrationId = pending.PendingRegistrationId,
            TokenId = pending.TokenId,
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
        var existing = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Email, pending.Email));

        if (existing != null)
        {
            // Update existing pending with new data
            existing.TokenId = pending.TokenId;
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
                TokenId = pending.TokenId,
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
            .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Email, email), cancellationToken);

        if (entity == null)
            return null;

        return new PendingRegistration
        {
            PendingRegistrationId = entity.PendingRegistrationId,
            TokenId = entity.TokenId,
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
            entity.TokenId = pending.TokenId;
            entity.Email = pending.Email;
            entity.PasswordHash = pending.PasswordHash;
            entity.Salt = pending.Salt;
            entity.PasswordHashVersion = pending.PasswordHashVersion;
            entity.TokenCreatedUtc = pending.TokenCreatedUtc;
            entity.AcceptedRules = pending.AcceptedRules;
            await _dbContext.SaveChangesAsync();
        }
    }
}
