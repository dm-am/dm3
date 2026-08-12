using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.PasswordChange;

/// <inheritdoc />
internal class PasswordChangeService : IPasswordChangeService
{
    private readonly IValidator<UserPasswordChange> _validator;
    private readonly IPasswordChangeRepository _repository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly ICompromisedPasswordChecker _compromisedPasswordChecker;
    private readonly IEventProducer _eventProducer;
    private readonly IPasswordChangeMailSender _notificationSender;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TokenConfiguration _tokenConfig;

    /// <inheritdoc />
    public PasswordChangeService(
        IValidator<UserPasswordChange> validator,
        ISecurityManager securityManager,
        ICompromisedPasswordChecker compromisedPasswordChecker,
        IPasswordChangeRepository repository,
        IAuthenticationService authenticationService,
        IIdentityProvider identityProvider,
        IEventProducer eventProducer,
        IPasswordChangeMailSender notificationSender,
        ISecurityAuditRepository auditService,
        IDateTimeProvider dateTimeProvider,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _repository = repository;
        _authenticationService = authenticationService;
        _identityProvider = identityProvider;
        _securityManager = securityManager;
        _compromisedPasswordChecker = compromisedPasswordChecker;
        _eventProducer = eventProducer;
        _notificationSender = notificationSender;
        _auditService = auditService;
        _dateTimeProvider = dateTimeProvider;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PasswordResetTokenInfo?> GetTokenInfo(Guid tokenId)
    {
        var tokenMinCreatedUtc = _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.PasswordResetTokenLifetimeHours);
        var isValid = await _repository.TokenValid(tokenId, tokenMinCreatedUtc);

        if (!isValid)
        {
            // Token doesn't exist, is removed, or is too old
            // Check if it exists at all (might be expired vs not found)
            var user = await _repository.FindUser(tokenId);
            return user == null ? null : PasswordResetTokenInfo.Expired();
        }

        return PasswordResetTokenInfo.Ready();
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserPasswordChange passwordChange)
    {
        // For OldPassword flow (no token), require authentication
        if (!passwordChange.Token.HasValue && !_identityProvider.Current.User.IsAuthenticated)
        {
            throw new HttpException(System.Net.HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        // Check that either token or oldPassword is provided (after auth check)
        if (!passwordChange.Token.HasValue && string.IsNullOrEmpty(passwordChange.OldPassword))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(passwordChange.OldPassword)] = "Введите текущий пароль"
            });
        }

        await _validator.ValidateAndThrowAsync(passwordChange);
        var user = passwordChange.Token.HasValue
            ? await _repository.FindUser(passwordChange.Token.Value)
            : _identityProvider.Current.User;

        if (user == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        // Check if new password matches current password
        if (_securityManager.ComparePasswords(passwordChange.NewPassword, user.Salt, user.PasswordHash))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(passwordChange.NewPassword)] = "Новый пароль совпадает с текущим"
            });
        }

        // NIST SP 800-63B: Check if password has been compromised in data breaches
        if (await _compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(passwordChange.NewPassword)] = RefusalMessage.PasswordBreached
            });
        }

        var (hash, salt) = _securityManager.GeneratePassword(passwordChange.NewPassword);
        await _repository.UpdatePassword(user.UserId, hash, salt, passwordChange.Token);

        // When changing via token, user is not authenticated - logout all sessions
        // When changing via old password, user is authenticated - keep current session
        //
        // The security journal is written in the same two branches, with two
        // types, because "changed from inside a session" and "reset through a
        // link out of the mailbox" are different events to the person working out
        // afterwards whether it was him. Nothing wrote either of them, so the
        // journal answered "nothing happened" to the one question it exists for.
        if (passwordChange.Token.HasValue)
        {
            await _authenticationService.LogoutAll(user.UserId);
            await _auditService.LogAsync(user.UserId, SecurityEventType.PasswordResetComplete);
        }
        else
        {
            await _authenticationService.LogoutElsewhere();
            await _auditService.LogAsync(user.UserId, SecurityEventType.PasswordChange);
        }

        // Audit logging: record password change event
        await _eventProducer.SendAsync(EventType.PasswordChanged, user.UserId);

        // Send notification email
        await _notificationSender.Send(user.Email ?? string.Empty, user.Username);

        return user;
    }
}
