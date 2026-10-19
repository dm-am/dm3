using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SessionService : ISessionService
{
    private readonly IAuthenticationRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SessionService(
        IAuthenticationRepository repository,
        IIdentityProvider identityProvider,
        ISecurityAuditRepository auditRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IIdentity> Logout()
    {
        var identity = _identityProvider.Current;
        await _repository.UpdateActivity(identity.User.UserId, _dateTimeProvider.Now);
        await _repository.RemoveSession(identity.User.UserId, identity.Session!.Id);
        await _auditRepository.LogAsync(identity.User.UserId, SecurityEventType.Logout);
        return Identity.Guest();
    }

    /// <inheritdoc />
    public async Task<IIdentity> LogoutElsewhere()
    {
        var identity = _identityProvider.Current;
        await _repository.RemoveSessionsExcept(identity.User.UserId, identity.Session!.Id);
        await _auditRepository.LogAsync(identity.User.UserId, SecurityEventType.LogoutElsewhere);

        return identity;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Session>> GetCurrentUserSessions()
    {
        var identity = _identityProvider.Current;
        return await _repository.GetUserSessions(identity.User.UserId, identity.Session?.Id);
    }

    /// <inheritdoc />
    public async Task TerminateSession(Guid userId, Guid sessionId)
    {
        var identity = _identityProvider.Current;

        if (identity.User.UserId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Завершить чужую сессию нельзя");
        }

        if (identity.Session?.Id == sessionId)
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                "Нельзя завершить текущую сессию. Для этого есть кнопка Выйти");
        }

        await _repository.RemoveSession(userId, sessionId);
        await _auditRepository.LogAsync(userId, SecurityEventType.SessionTerminated,
            details: $"SessionId: {sessionId}");
    }

    /// <inheritdoc />
    public async Task LogoutAll(Guid userId)
    {
        await _repository.RemoveAllSessions(userId);
    }
}
