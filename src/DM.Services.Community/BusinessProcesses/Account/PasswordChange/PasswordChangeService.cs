using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Account.PasswordChange.Confirmation;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange;

/// <inheritdoc />
internal class PasswordChangeService : IPasswordChangeService
{
    private readonly IValidator<UserPasswordChange> _validator;
    private readonly IPasswordChangeRepository _repository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IInvokedEventProducer _eventProducer;
    private readonly IPasswordChangeNotificationSender _notificationSender;

    /// <inheritdoc />
    public PasswordChangeService(
        IValidator<UserPasswordChange> validator,
        ISecurityManager securityManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IPasswordChangeRepository repository,
        IAuthenticationService authenticationService,
        IIdentityProvider identityProvider,
        IInvokedEventProducer eventProducer,
        IPasswordChangeNotificationSender notificationSender)
    {
        _validator = validator;
        _repository = repository;
        _authenticationService = authenticationService;
        _identityProvider = identityProvider;
        _securityManager = securityManager;
        _updateBuilderFactory = updateBuilderFactory;
        _eventProducer = eventProducer;
        _notificationSender = notificationSender;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserPasswordChange passwordChange)
    {
        await _validator.ValidateAndThrowAsync(passwordChange);
        var user = passwordChange.Token.HasValue
            ? await _repository.FindUser(passwordChange.Token.Value)
            : _identityProvider.Current.User;

        if (user == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "User not found");
        }

        // Check if password is reused (last 5 passwords)
        var isReused = await _repository.IsPasswordReused(user.UserId, passwordChange.NewPassword, 5, CancellationToken.None);
        if (isReused)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(passwordChange.NewPassword)] = "Вы не можете использовать один из последних 5 паролей"
            });
        }

        // Save current password to history before changing it
        await _repository.SavePasswordToHistory(user.UserId, user.PasswordHash, user.Salt, user.PasswordHashVersion, CancellationToken.None);

        var (hash, salt, version) = _securityManager.GeneratePassword(passwordChange.NewPassword);
        var userUpdate = _updateBuilderFactory.Create<User>(user.UserId)
            .Field(u => u.PasswordHash, hash)
            .Field(u => u.Salt, salt)
            .Field(u => u.PasswordHashVersion, version);
        var tokenUpdate = passwordChange.Token.HasValue
            ? _updateBuilderFactory.Create<Token>(passwordChange.Token.Value)
                .Field(t => t.IsRemoved, true)
            : null;

        await _repository.UpdatePassword(userUpdate, tokenUpdate);

        // Cleanup old password history entries (keep max 10)
        await _repository.CleanupOldEntries(user.UserId, 10, CancellationToken.None);

        // When changing via token, user is not authenticated - logout all sessions
        // When changing via old password, user is authenticated - keep current session
        if (passwordChange.Token.HasValue)
        {
            await _authenticationService.LogoutAll(user.UserId);
        }
        else
        {
            await _authenticationService.LogoutElsewhere();
        }

        // Audit logging: record password change event
        await _eventProducer.Send(EventType.PasswordChanged, user.UserId);

        // Send notification email
        await _notificationSender.Send(user.Email ?? string.Empty, user.Login);

        return user;
    }
}