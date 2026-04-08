using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using DbSession = DM.Infrastructure.Persistence.Entities.Account.Session;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;
using Session = DM.Domain.Core.Identity.Session;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc cref="IAuthenticationRepository" />
internal class AuthenticationRepository : MongoRepository, IAuthenticationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AuthenticationRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IMapper mapper) : base(mongoClient)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<(bool Success, AuthenticatedUser? User)> TryFindUserByEmail(string email)
    {
        var result = await _dbContext.Users
            .TagWith("DM.Authentication.TryFindUserByEmail")
            .Where(u => u.Email.ToLower() == email.ToLower())
            .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
        return (result != null, result);
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(Guid userId)
    {
        return _dbContext.Users
            .TagWith("DM.Authentication.FindUser")
            .Where(u => u.UserId == userId)
            .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Session?> FindUserSession(Guid sessionId)
    {
        var userSessions = await Collection<UserSession>()
            .Find(Filter<UserSession>()
                .ElemMatch(u => u.Sessions, s => s.Id == sessionId))
            .FirstOrDefaultAsync();
        var matchingSession = userSessions?.Sessions.FirstOrDefault(s => s.Id == sessionId);
        return matchingSession == null
            ? null
            : _mapper.Map<Session>(matchingSession);
    }

    /// <inheritdoc />
    public async Task<UserSettings> FindUserSettings(Guid userId)
    {
        var dbSettings = await Collection<DbUserSettings>()
            .Find(Filter<DbUserSettings>()
                .Eq(u => u.UserId, userId))
            .FirstOrDefaultAsync();

        if (dbSettings == null)
            return UserSettings.Default;

        return new UserSettings
        {
            Id = dbSettings.UserId,
            Theme = dbSettings.Theme,
            Paging = new PagingSettings
            {
                PostsPerPage = dbSettings.Paging.PostsPerPage,
                CommentsPerPage = dbSettings.Paging.CommentsPerPage,
                MessagesPerPage = dbSettings.Paging.MessagesPerPage,
                TopicsPerPage = dbSettings.Paging.TopicsPerPage,
                EntitiesPerPage = dbSettings.Paging.EntitiesPerPage
            }
        };
    }

    /// <inheritdoc />
    public Task RemoveSession(Guid userId, Guid sessionId)
    {
        return Collection<UserSession>().FindOneAndUpdateAsync(
            Filter<UserSession>().Eq(u => u.Id, userId),
            Update<UserSession>().PullFilter(s => s.Sessions, s => s.Id == sessionId));
    }

    /// <inheritdoc />
    public Task RefreshSession(Guid userId, Guid sessionId, DateTimeOffset expirationDate)
    {
        return Collection<UserSession>().FindOneAndUpdateAsync(
            Filter<UserSession>().Eq(u => u.Id, userId) &
            Filter<UserSession>().ElemMatch(u => u.Sessions, s => s.Id == sessionId),
            Update<UserSession>().Set(u => u.Sessions[-1].ExpirationUtc, expirationDate.UtcDateTime));
    }

    /// <inheritdoc />
    public async Task<Session> AddSession(Guid userId, CreateSession session)
    {
        var dbSession = new DbSession
        {
            Id = session.Id,
            ExpirationUtc = session.ExpirationUtc,
            Persistent = session.Persistent,
            Invisible = session.Invisible,
            CreatedUtc = session.CreatedUtc,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            DeviceInfo = session.DeviceInfo
        };
        await Collection<UserSession>().FindOneAndUpdateAsync(
            Filter<UserSession>().Eq(u => u.Id, userId),
            Update<UserSession>().Push(s => s.Sessions, dbSession),
            new FindOneAndUpdateOptions<UserSession> { IsUpsert = true });
        return _mapper.Map<Session>(dbSession);
    }

    /// <inheritdoc />
    public Task RemoveSessionsExcept(Guid userId, Guid sessionId)
    {
        return Collection<UserSession>().FindOneAndUpdateAsync(
            Filter<UserSession>().Eq(u => u.Id, userId),
            Update<UserSession>().PullFilter(s => s.Sessions, s => s.Id != sessionId),
            new FindOneAndUpdateOptions<UserSession> { IsUpsert = true });
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
        var userSessions = await Collection<UserSession>()
            .Find(Filter<UserSession>().Eq(u => u.Id, userId))
            .FirstOrDefaultAsync();

        if (userSessions?.Sessions == null)
        {
            return Array.Empty<Session>();
        }

        return userSessions.Sessions
            .Select(s =>
            {
                var session = _mapper.Map<Session>(s);
                session.IsCurrent = currentSessionId.HasValue && s.Id == currentSessionId.Value;
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
        return Collection<UserSession>().FindOneAndUpdateAsync(
            Filter<UserSession>().Eq(u => u.Id, userId),
            Update<UserSession>().Set(u => u.Sessions, new List<DbSession>()));
    }
}
