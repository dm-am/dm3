using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc />
internal class DeactivationService : IDeactivationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly IAuthenticationService _authenticationService;
    private readonly IEventProducer _eventProducer;
    private readonly IDeactivationRepository _deactivationRepository;

    public DeactivationService(
        IIdentityProvider identityProvider,
        ISecurityManager securityManager,
        IAuthenticationService authenticationService,
        IEventProducer eventProducer,
        IDeactivationRepository deactivationRepository)
    {
        _identityProvider = identityProvider;
        _securityManager = securityManager;
        _authenticationService = authenticationService;
        _eventProducer = eventProducer;
        _deactivationRepository = deactivationRepository;
    }

    /// <inheritdoc />
    public async Task Deactivate(string password)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
            throw new HttpException(HttpStatusCode.Unauthorized, "Требуется авторизация");

        // Load user credentials for password verification
        var credentials = await _deactivationRepository.GetUserCredentials(currentUser.UserId);

        if (credentials == null)
            throw new HttpException(HttpStatusCode.NotFound, "Пользователь не найден");

        // Verify password
        if (!_securityManager.ComparePasswords(password, credentials.Salt, credentials.PasswordHash))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["password"] = "Неверный пароль"
            });
        }

        // Soft delete: set IsRemoved = true
        await _deactivationRepository.DeactivateUser(credentials.UserId);

        // Terminate all active sessions
        await _authenticationService.LogoutAll(credentials.UserId);

        // Audit logging: record deactivation event
        await _eventProducer.SendAsync(EventType.AccountDeactivated, credentials.UserId);
    }
}
