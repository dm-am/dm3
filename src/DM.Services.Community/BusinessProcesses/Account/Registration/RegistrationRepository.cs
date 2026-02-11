using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// Registration repository for email-first registration flow
/// </summary>
internal class RegistrationRepository : IRegistrationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegistrationRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
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
    public async Task<bool> LoginFree(string login, CancellationToken cancellationToken) =>
        !await _dbContext.Users.AnyAsync(u => EF.Functions.ILike(u.Login, login), cancellationToken) &&
        !await _dbContext.LoginHistories.AnyAsync(h => EF.Functions.ILike(h.OldLogin, login), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> PendingExists(string email, CancellationToken cancellationToken) =>
        await _dbContext.PendingRegistrations
            .AnyAsync(p => EF.Functions.ILike(p.Email, email), cancellationToken);

    /// <inheritdoc />
    public async Task AddPending(PendingRegistration pending)
    {
        _dbContext.PendingRegistrations.Add(pending);
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
            _dbContext.PendingRegistrations.Add(pending);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByEmail(string email, CancellationToken cancellationToken) =>
        await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Email, email), cancellationToken);

    /// <inheritdoc />
    public async Task UpdatePending(PendingRegistration pending)
    {
        _dbContext.PendingRegistrations.Update(pending);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task SavePasswordToHistory(Guid userId, string passwordHash, string salt, int version, CancellationToken ct)
    {
        var historyEntry = new PasswordHistory
        {
            PasswordHistoryId = _guidFactory.Create(),
            UserId = userId,
            PasswordHash = passwordHash,
            Salt = salt,
            PasswordHashVersion = version,
            CreatedUtc = _dateTimeProvider.Now
        };

        _dbContext.PasswordHistories.Add(historyEntry);
        await _dbContext.SaveChangesAsync(ct);
    }
}
