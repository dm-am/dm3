using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
internal class TwoFactorRemovalService : ITwoFactorRemovalService
{
    private readonly ITwoFactorRepository _repository;
    private readonly IConfirmationTokenRepository _tokens;
    private readonly ITwoFactorRemovalMailSender _mailSender;
    private readonly ITokenFactory _tokenFactory;
    private readonly ISessionService _sessionService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TwoFactorRemovalService> _logger;
    private readonly TwoFactorConfiguration _config;
    private readonly TokenConfiguration _tokenConfig;

    public TwoFactorRemovalService(
        ITwoFactorRepository repository,
        IConfirmationTokenRepository tokens,
        ITwoFactorRemovalMailSender mailSender,
        ITokenFactory tokenFactory,
        ISessionService sessionService,
        IIdentityProvider identityProvider,
        ISecurityAuditRepository auditRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<TwoFactorRemovalService> logger,
        IOptions<TwoFactorConfiguration> config,
        IOptions<TokenConfiguration> tokenConfig)
    {
        _repository = repository;
        _tokens = tokens;
        _mailSender = mailSender;
        _tokenFactory = tokenFactory;
        _sessionService = sessionService;
        _identityProvider = identityProvider;
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _config = config.Value;
        _tokenConfig = tokenConfig.Value;
    }

    /// <inheritdoc />
    public async Task Request(string email, CancellationToken cancellationToken = default)
    {
        var account = await _repository.FindAccountByEmail(email, cancellationToken);
        if (account?.Email == null)
        {
            _logger.LogInformation("Two-factor removal requested for an address with no account");
            return;
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            _logger.LogInformation("Two-factor removal requested for an account without a factor");
            return;
        }

        var token = _tokenFactory.Create(account.UserId, TokenType.TwoFactorRemovalRequest);
        await _tokens.ReplaceToken(account.UserId, token, cancellationToken);
        await _mailSender.SendRequest(account.Email, token.Secret);
    }

    /// <inheritdoc />
    public async Task Schedule(Guid secret, CancellationToken cancellationToken = default)
    {
        var liveSince = _dateTimeProvider.Now -
                        TimeSpan.FromHours(_tokenConfig.TwoFactorRemovalTokenLifetimeHours);
        var userId = await _tokens.RedeemToken(
            secret, TokenType.TwoFactorRemovalRequest, liveSince, cancellationToken);
        if (userId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        // An account that is gone answers as a link that is gone (SECURITY.md).
        var account = await _repository.FindAccount(userId.Value, cancellationToken);
        if (account?.Email == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        var dueUtc = _dateTimeProvider.Now + TimeSpan.FromDays(_config.RemovalDelayDays);
        if (!await _repository.ScheduleRemoval(account.UserId, dueUtc, cancellationToken))
        {
            // The read above is a hint and this write is the decision.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        // Every session goes, in case somebody else set this in motion.
        await _sessionService.LogoutAll(account.UserId);

        var cancellation = _tokenFactory.Create(account.UserId, TokenType.TwoFactorRemovalCancellation);
        await _tokens.ReplaceToken(account.UserId, cancellation, cancellationToken);

        await _auditRepository.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovalScheduled);
        await _mailSender.SendScheduled(
            account.Email, dueUtc, cancellation.Secret, account.TimeZoneId);
    }

    /// <inheritdoc />
    public async Task Cancel(Guid secret, CancellationToken cancellationToken = default)
    {
        // No lifetime: live for as long as there is a removal to call off.
        var userId = await _tokens.RedeemToken(
            secret, TokenType.TwoFactorRemovalCancellation, null, cancellationToken);
        if (userId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        if (!await _repository.CancelScheduledRemoval(userId.Value, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.LinkInvalidOrUsed);
        }

        await _auditRepository.LogAsync(userId.Value, SecurityEventType.TwoFactorRemovalCancelled);
    }

    /// <inheritdoc />
    public async Task<int> RunDue(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var due = await _repository.FindRemovalsDue(now, cancellationToken);
        var removed = 0;
        foreach (var userId in due)
        {
            // The listing is a hint and the write is the decision: a cancellation
            // can land between the two.
            if (!await _repository.RemoveIfDue(userId, now, cancellationToken))
            {
                continue;
            }

            removed++;
            // The cancellation link dies with the removal it could have called off.
            await _tokens.RevokeTokens(userId, TokenType.TwoFactorRemovalCancellation, cancellationToken);
            await _auditRepository.LogAsync(userId, SecurityEventType.TwoFactorDisabled,
                details: "Отложенное отключение по почте");
            _logger.LogInformation("Two-factor removed after the waiting period. UserId={UserId}", userId);
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task ClearForColleague(string username, CancellationToken cancellationToken = default)
    {
        var caller = _identityProvider.RequireAuthenticated();

        // Compared here rather than through an intention, so the refusal names the
        // rank. The role is the folded one: an administrator who owes a factor cannot
        // hand a colleague the way in.
        if (caller.Role < UserRole.Admin)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Отключить двухфакторную аутентификацию может только администратор");
        }

        var account = await _repository.FindAccountByUsername(username, cancellationToken);
        if (account == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        if (account.UserId == caller.UserId)
        {
            // One's own factor through the staff door would make it a single-administrator
            // arrangement; the owner's own path is the settings screen.
            throw new HttpException(HttpStatusCode.Forbidden,
                "Отключить свою двухфакторную аутентификацию можно в настройках");
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        await _repository.Remove(account.UserId, cancellationToken);
        await _sessionService.LogoutAll(account.UserId);

        // Both journals: the owner's and the administrator's. Past the rank check the caller is an
        // administrator: the System identity never signs in.
        await _auditRepository.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Администратор: {caller.Username}");
        await _auditRepository.LogAsync(caller.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Учетная запись: {account.Username}, роль администратора: Администратор");

        _logger.LogWarning(
            "Two-factor cleared by an administrator. Target={UserId}, Actor={ActorId}",
            account.UserId, caller.UserId);
    }
}
