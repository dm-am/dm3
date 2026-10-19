using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc cref="IAuthenticationRepository" />
internal class AuthenticationRepository : IAuthenticationRepository
{
    private readonly DmDbContext _dbContext;

    public AuthenticationRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Past the global filter: the sign-in refuses a removed account as removed after
    /// a proven password (SECURITY.md).
    /// </remarks>
    public async Task<(bool Success, AuthenticatedUser? User)> TryFindUserByEmail(string email)
    {
        var result = await _dbContext.Users
            .IgnoreQueryFilters()
            .TagWith("DM.Authentication.TryFindUserByEmail")
            .Where(AccountNames.EmailIs(email))
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync();
        return (result != null, result);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Past the filter for the same reason as the lookup by address: the code
    /// step and the session paths name a removed account instead of treating
    /// it as one that never was.
    /// </remarks>
    public Task<AuthenticatedUser?> FindUser(Guid userId)
    {
        return _dbContext.Users
            .IgnoreQueryFilters()
            .TagWith("DM.Authentication.FindUser")
            .Where(u => u.UserId == userId)
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Session?> FindUserSession(Guid userId, Guid sessionId)
    {
        // Both halves of the token in one predicate: a session id that exists
        // but belongs to somebody else must not authenticate. The
        // hottest query on the site stays a primary-key lookup — the owner
        // check narrows a set of one.
        var session = await _dbContext.UserSessions
            .TagWith("DM.Authentication.FindUserSession")
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId);
        return session == null
            ? null
            : session.ToSession();
    }

    /// <inheritdoc />
    public async Task<UserSettings> FindUserSettings(Guid userId)
    {
        // No row means the user never saved settings: the defaults.
        var dbSettings = await _dbContext.UserSettings
            .TagWith("DM.Authentication.FindUserSettings")
            .FirstOrDefaultAsync(s => s.UserId == userId);

        return dbSettings == null ? UserSettings.Default : dbSettings.ToUserSettings();
    }

    /// <inheritdoc />
    public Task RemoveSession(Guid userId, Guid sessionId)
    {
        return _dbContext.UserSessions
            .Where(s => s.SessionId == sessionId && s.UserId == userId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public Task RefreshSession(Guid userId, Guid sessionId, DateTimeOffset expirationDate)
    {
        return _dbContext.UserSessions
            .Where(s => s.SessionId == sessionId && s.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpirationUtc, expirationDate));
    }

    /// <inheritdoc />
    public async Task<Session> AddSession(Guid userId, CreateSession session)
    {
        var dbSession = new UserSession
        {
            SessionId = session.Id,
            UserId = userId,
            ExpirationUtc = session.ExpirationUtc,
            Persistent = session.Persistent,
            CreatedUtc = session.CreatedUtc,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            DeviceInfo = session.DeviceInfo
        };
        _dbContext.UserSessions.Add(dbSession);
        await _dbContext.SaveChangesAsync();
        return dbSession.ToSession();
    }

    /// <inheritdoc />
    public Task RemoveSessionsExcept(Guid userId, Guid sessionId)
    {
        return _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.SessionId != sessionId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task UpdateActivity(Guid userId, DateTimeOffset lastActivityUtc)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastActivityUtc = lastActivityUtc;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Session>> GetUserSessions(Guid userId, Guid? currentSessionId = null)
    {
        var userSessions = await _dbContext.UserSessions
            .TagWith("DM.Authentication.UserSessions")
            .Where(s => s.UserId == userId)
            .ToListAsync();

        return userSessions
            .Select(s =>
            {
                var session = s.ToSession();
                session.IsCurrent = currentSessionId.HasValue && s.SessionId == currentSessionId.Value;
                return session;
            })
            .OrderByDescending(s => s.IsCurrent)
            .ThenByDescending(s => s.CreatedUtc)
            .ToList();
    }

    /// <inheritdoc />
    public Task<bool> IsPendingRegistration(string email)
    {
        return _dbContext.PendingRegistrations
            .TagWith("DM.Authentication.IsPendingRegistration")
            .AnyAsync(p => p.Email.ToLower() == email.ToLower());
    }

    /// <inheritdoc />
    public Task RemoveAllSessions(Guid userId)
    {
        return _dbContext.UserSessions
            .Where(s => s.UserId == userId)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public async Task<SessionPurgeResult> PurgeExpiredSessions(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var removed = await _dbContext.UserSessions
            .Where(s => s.ExpirationUtc < now)
            .ExecuteDeleteAsync(cancellationToken);

        return new SessionPurgeResult(removed);
    }
}
