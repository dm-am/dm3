using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
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
    private readonly ISessionService _sessionService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly ICompromisedPasswordChecker _compromisedPasswordChecker;
    private readonly IEventProducer _eventProducer;
    private readonly IPasswordChangeMailSender _notificationSender;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITwoFactorRepository _twoFactorRepository;
    private readonly TokenConfiguration _tokenConfig;

    public PasswordChangeService(
        IValidator<UserPasswordChange> validator,
        ISecurityManager securityManager,
        ICompromisedPasswordChecker compromisedPasswordChecker,
        IPasswordChangeRepository repository,
        ISessionService sessionService,
        IIdentityProvider identityProvider,
        IEventProducer eventProducer,
        IPasswordChangeMailSender notificationSender,
        ISecurityAuditRepository auditRepository,
        IDateTimeProvider dateTimeProvider,
        ITwoFactorRepository twoFactorRepository,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _repository = repository;
        _sessionService = sessionService;
        _identityProvider = identityProvider;
        _securityManager = securityManager;
        _compromisedPasswordChecker = compromisedPasswordChecker;
        _eventProducer = eventProducer;
        _notificationSender = notificationSender;
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
        _twoFactorRepository = twoFactorRepository;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PasswordResetTokenInfo?> GetTokenInfo(Guid secret)
    {
        var liveSince = _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.PasswordResetTokenLifetimeHours);
        if (await _repository.TokenValid(secret, liveSince))
        {
            return PasswordResetTokenInfo.Ready();
        }

        // Live and expired are different answers on the form: one opens the field,
        // the other offers to request a new letter. Anything older than the window
        // still in the table is an expired one; a secret nobody issued is null.
        var expired = await _repository.FindUser(secret, DateTimeOffset.MinValue);
        return expired == null ? null : PasswordResetTokenInfo.Expired();
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
        // The window is checked here as well as in the repository: a reset opened
        // with an expired secret must not fall through to the authenticated branch.
        var liveSince = _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.PasswordResetTokenLifetimeHours);
        var user = passwordChange.Token.HasValue
            ? await _repository.FindUser(passwordChange.Token.Value, liveSince)
            : _identityProvider.Current.User;

        if (user == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        await RefuseUnfitPassword(user, passwordChange.NewPassword);

        var (hash, salt) = _securityManager.GeneratePassword(passwordChange.NewPassword);
        await _repository.UpdatePassword(user.UserId, hash, salt, passwordChange.Token);

        // Any login begun with the old password dies with it, on both routes.
        await _twoFactorRepository.RemoveChallengesOf(user.UserId);

        // Via token every session ends, via the old password the current one stays;
        // the journal tells the two events apart.
        if (passwordChange.Token.HasValue)
        {
            await _sessionService.LogoutAll(user.UserId);
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.PasswordResetComplete);
        }
        else
        {
            await _sessionService.LogoutElsewhere();
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.PasswordChange);
        }

        await _eventProducer.SendAsync(EventType.PasswordChanged, user.UserId);
        await _notificationSender.Send(user.Email!);

        return user;
    }

    /// <summary>
    /// Refuses a new password the account must not take: the one it already
    /// has, and one that has turned up in a breach.
    /// </summary>
    private async Task RefuseUnfitPassword(AuthenticatedUser user, string newPassword)
    {
        if (_securityManager.ComparePasswords(newPassword, user.Salt, user.PasswordHash))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(UserPasswordChange.NewPassword)] = "Новый пароль совпадает с текущим"
            });
        }

        await _compromisedPasswordChecker.ThrowIfCompromised(newPassword, nameof(UserPasswordChange.NewPassword));
    }
}
