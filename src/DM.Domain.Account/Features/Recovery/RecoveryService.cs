using System.Threading;
using DM.Domain.Core.Tokens;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Account.Features.Recovery;

/// <inheritdoc />
internal class RecoveryService : IRecoveryService
{
    private readonly IEmailLookupRepository _emailLookupRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IPasswordResetMailSender _passwordResetEmailSender;
    private readonly IRegistrationMailSender _activationEmailSender;
    private readonly ITokenFactory _tokenFactory;
    private readonly IGuidFactory _guidFactory;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RecoveryService> _logger;

    public RecoveryService(
        IEmailLookupRepository emailLookupRepository,
        IRegistrationRepository registrationRepository,
        IPasswordResetRepository passwordResetRepository,
        IPasswordResetMailSender passwordResetEmailSender,
        IRegistrationMailSender activationEmailSender,
        ITokenFactory tokenFactory,
        IGuidFactory guidFactory,
        ISecurityAuditRepository auditService,
        IDateTimeProvider dateTimeProvider,
        ILogger<RecoveryService> logger)
    {
        _emailLookupRepository = emailLookupRepository;
        _registrationRepository = registrationRepository;
        _passwordResetRepository = passwordResetRepository;
        _passwordResetEmailSender = passwordResetEmailSender;
        _activationEmailSender = activationEmailSender;
        _tokenFactory = tokenFactory;
        _guidFactory = guidFactory;
        _auditService = auditService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RecoveryResult> Recover(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Check if email belongs to an active user
        var user = await _emailLookupRepository.GetUserByEmail(normalizedEmail);
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            // Send password reset email
            var token = _tokenFactory.Create(user.UserId, TokenType.PasswordChange);
            await _passwordResetRepository.ReplacePasswordResetToken(user.UserId, token);
            // Secret, not TokenId: the row keeps a hash of this value and nothing
            // that would let a reader of the table redeem the link.
            await _passwordResetEmailSender.Send(user.Email, user.Username, token.Secret);

            // The journal the owner of the account reads: a reset he did not ask
            // for is the visible half of somebody working on his mailbox, and the
            // application log is not a place he can look.
            await _auditService.LogAsync(user.UserId, SecurityEventType.PasswordResetRequest);

            _logger.LogInformation("Password reset email sent for active user");
            return RecoveryResult.PasswordReset;
        }

        // Check if email belongs to a pending registration
        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, CancellationToken.None);
        if (pending != null)
        {
            // Resend activation email with new token
            // A resend issues a new secret rather than mailing the old one again:
            // the old value is not recoverable from the row by design.
            pending.Secret = _guidFactory.Create();
            pending.SecretHash = ConfirmationSecret.Hash(pending.Secret);
            pending.TokenCreatedUtc = _dateTimeProvider.Now;
            await _registrationRepository.UpdatePending(pending);
            await _activationEmailSender.Send(pending.Email, pending.Secret);

            _logger.LogInformation("Activation email resent for pending registration");
            return RecoveryResult.ActivationResent;
        }

        // Email not found
        _logger.LogInformation("Recovery requested for unknown email");
        return RecoveryResult.NotFound;
    }
}
