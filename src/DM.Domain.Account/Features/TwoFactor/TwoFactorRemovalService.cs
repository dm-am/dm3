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
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TwoFactorRemovalService> _logger;
    private readonly TwoFactorConfiguration _config;
    private readonly TokenConfiguration _tokenConfig;

    public TwoFactorRemovalService(
        ITwoFactorRepository repository,
        IConfirmationTokenRepository tokens,
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

        // An account that is gone answers as a link that is gone: a page reached
        // from a letter speaks of the link and of the reader's action, never of
        // the state of the account (SECURITY.md).
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
            // The read above is a hint and this write is the decision: the factor
            // can be switched off in the moment between them. Everything below
            // belongs to a waiting period that was never started - the sessions,
            // the journal entry, and a letter naming a date and offering a link
            // that would answer "already gone" when it was followed.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        // Every session goes. Whoever set this in motion is either the owner,
        // who is about to sign in again anyway, or somebody working on the
        // account - and for the second one this is the point.
        await _authenticationService.LogoutAll(account.UserId);

        var cancellation = _tokenFactory.Create(account.UserId, TokenType.TwoFactorRemovalCancellation);
        await _tokens.ReplaceToken(account.UserId, cancellation, cancellationToken);

        await _auditService.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovalScheduled);
        await _mailSender.SendScheduled(
            account.Email, dueUtc, cancellation.Secret, account.TimeZoneId);
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
        var now = _dateTimeProvider.Now;
        var due = await _repository.FindRemovalsDue(now, cancellationToken);
        var removed = 0;
        foreach (var userId in due)
        {
            // The listing is a hint and the write is the decision. Calling the
            // removal off - the second link out of the letter, or any sign-in that
            // passes the factor - clears the due date, and it lands between the
            // two often enough to matter: a person who has just kept their factor
            // must not lose it to a pass that had already made up its mind. The
            // journal entry and the log line follow the write for the same reason.
            if (!await _repository.RemoveIfDue(userId, now, cancellationToken))
            {
                continue;
            }

            removed++;
            // The link that could have called this off dies with the removal:
            // opened afterwards, it would answer "called off" about a factor
            // that is already gone, and the owner would relax over an open door.
            await _tokens.RevokeTokens(userId, TokenType.TwoFactorRemovalCancellation, cancellationToken);
            await _auditService.LogAsync(userId, SecurityEventType.TwoFactorDisabled,
                details: "Отложенное отключение по почте");
            _logger.LogInformation("Two-factor removed after the waiting period. UserId={UserId}", userId);
        }

        return removed;
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
                "Отключить двухфакторную аутентификацию может только администратор");
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
                "Отключить свою двухфакторную аутентификацию можно в настройках");
        }

        if (!await _repository.IsConfirmed(account.UserId, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        await _repository.Remove(account.UserId, cancellationToken);
        await _authenticationService.LogoutAll(account.UserId);

        // Both journals. The owner has to see what was done to his account, and
        // the administrator's own trail has to carry what he did and under which
        // rank.
        await _auditService.LogAsync(account.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Администратор: {caller.Username}");
        await _auditService.LogAsync(caller.UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            details: $"Учетная запись: {account.Username}; роль администратора: {caller.Role}");

        _logger.LogWarning(
            "Two-factor cleared by an administrator. Target={UserId}, Actor={ActorId}",
            account.UserId, caller.UserId);
    }
}
