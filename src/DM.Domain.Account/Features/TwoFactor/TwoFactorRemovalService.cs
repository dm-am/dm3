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
    private readonly ITwoFactorRemovalTokenRepository _tokens;
    private readonly ITwoFactorRemovalMailSender _mailSender;
    private readonly ITokenFactory _tokenFactory;
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TwoFactorRemovalService> _logger;
    private readonly TwoFactorConfiguration _config;
    private readonly TokenConfiguration _tokenConfig;

    public TwoFactorRemovalService(
        ITwoFactorRepository repository,
        ITwoFactorRemovalTokenRepository tokens,
        ITwoFactorRemovalMailSender mailSender,
        ITokenFactory tokenFactory,
        IAuthenticationService authenticationService,
        IIdentityProvider identityProvider,
        ISecurityAuditRepository auditService,
        IDateTimeProvider dateTimeProvider,
        ILogger<TwoFactorRemovalService> logger,
        IOptions<TwoFactorConfiguration> config,
        IOptions<TokenConfiguration> tokenConfig)
    {
        _repository = repository;
        _tokens = tokens;
        _mailSender = mailSender;
        _tokenFactory = tokenFactory;
        _authenticationService = authenticationService;
        _identityProvider = identityProvider;
        _auditService = auditService;
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

        if (TwoFactorRequirement.AppliesTo(account.Role))
        {
            // The mailed path is closed for these ranks and nothing is issued.
            // The refusal is written into the account's own journal rather than
            // into the answer: the answer is the same for every address, and
            // the owner of this one has somewhere to read it. Under its own type:
            // written as a scheduled removal, the entry sent the owner looking
            // for a waiting period that was never started.
            await _auditService.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovalRefused,
                details: "Снятие по почте закрыто для этой роли");
            _logger.LogWarning(
                "Two-factor removal by mail refused by rank. UserId={UserId}", account.UserId);
            return;
        }

        var token = _tokenFactory.Create(account.UserId, TokenType.TwoFactorRemovalRequest);
        await _tokens.ReplaceToken(account.UserId, token, cancellationToken);
        await _mailSender.SendRequest(account.Email, account.Username, token.Secret);
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

        var account = await _repository.FindAccount(userId.Value, cancellationToken);
        if (account?.Email == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        // Asked again although the request refused this rank already: a person
        // promoted between the letter and the click would otherwise carry a live
        // link past the rule.
        if (TwoFactorRequirement.AppliesTo(account.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.TwoFactorMailRemovalClosed);
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        var dueUtc = _dateTimeProvider.Now + TimeSpan.FromDays(_config.RemovalDelayDays);
        await _repository.ScheduleRemoval(account.UserId, dueUtc, cancellationToken);

        // Every session goes. Whoever set this in motion is either the owner,
        // who is about to sign in again anyway, or somebody working on the
        // account - and for the second one this is the point.
        await _authenticationService.LogoutAll(account.UserId);

        var cancellation = _tokenFactory.Create(account.UserId, TokenType.TwoFactorRemovalCancellation);
        await _tokens.ReplaceToken(account.UserId, cancellation, cancellationToken);

        await _auditService.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovalScheduled);
        await _mailSender.SendScheduled(account.Email, account.Username, dueUtc, cancellation.Secret);
    }

    /// <inheritdoc />
    public async Task Cancel(Guid secret, CancellationToken cancellationToken = default)
    {
        // No lifetime on this one: it is live for exactly as long as there is a
        // removal to call off, and the refusal below is what says so.
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

        await _auditService.LogAsync(userId.Value, SecurityEventType.TwoFactorRemovalCancelled);
    }

    /// <inheritdoc />
    public async Task<int> RunDue(CancellationToken cancellationToken = default)
    {
        var due = await _repository.FindRemovalsDue(_dateTimeProvider.Now, cancellationToken);
        foreach (var userId in due)
        {
            await _repository.Remove(userId, cancellationToken);
            await _auditService.LogAsync(userId, SecurityEventType.TwoFactorDisabled,
                details: "Отложенное снятие по почте");
            _logger.LogInformation("Two-factor removed after the waiting period. UserId={UserId}", userId);
        }

        return due.Count;
    }

    /// <inheritdoc />
    public async Task ClearForColleague(string username, CancellationToken cancellationToken = default)
    {
        var caller = _identityProvider.Current.User;
        if (!caller.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        // Compared here rather than through an intention, the way the rest of
        // moderation compares a rank: the refusal is read by a member of staff
        // about their own authority, and a general sentence would leave them
        // unable to tell a missing rank from a broken screen.
        //
        // The role read here is the folded one, so an administrator who owes a
        // factor cannot use this to hand a colleague their way in - which is the
        // point of folding it in one place.
        if (caller.Role < UserRole.Admin)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Снять второй фактор может только администратор");
        }

        var account = await _repository.FindAccountByUsername(username, cancellationToken);
        if (account == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        if (account.UserId == caller.UserId)
        {
            // Taking one's own factor off through the staff door would make the
            // arrangement a single-administrator one, which is the thing it
            // exists not to be. The owner's own path is the settings screen.
            throw new HttpException(HttpStatusCode.Forbidden,
                "Свой второй фактор снимают в настройках");
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        await _repository.Remove(account.UserId, cancellationToken);
        await _authenticationService.LogoutAll(account.UserId);

        // Both journals. The owner has to see what was done to his account, and
        // the administrator's own trail has to carry what he did - under his real
        // rank, which is what the recorded role is kept for.
        await _auditService.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Администратор: {caller.Username}");
        await _auditService.LogAsync(caller.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Учетная запись: {account.Username}; роль администратора: {caller.RecordedRole}");

        _logger.LogWarning(
            "Two-factor cleared by an administrator. Target={UserId}, Actor={ActorId}",
            account.UserId, caller.UserId);
    }
}
