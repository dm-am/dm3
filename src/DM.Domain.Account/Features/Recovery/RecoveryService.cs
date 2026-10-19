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
    private readonly ISecurityAuditRepository _auditRepository;
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
        ISecurityAuditRepository auditRepository,
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
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RecoveryResult> Recover(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _emailLookupRepository.GetUserByEmail(normalizedEmail);

        // A removed account answers exactly as a live one, but nothing is mailed:
        // the way back runs through support.
        if (user != null && user.IsRemoved && !string.IsNullOrEmpty(user.Email))
        {
            _logger.LogInformation("Recovery requested for a removed account");
            return RecoveryResult.PasswordReset;
        }

        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            var token = _tokenFactory.Create(user.UserId, TokenType.PasswordChange);
            await _passwordResetRepository.ReplacePasswordResetToken(user.UserId, token);
            // Secret, not TokenId: the row keeps a hash of this value and nothing
            // that would let a reader of the table redeem the link.
            await _passwordResetEmailSender.Send(user.Email, token.Secret);

            // The owner reads the journal: a reset he did not ask for is a warning.
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.PasswordResetRequest);

            _logger.LogInformation("Password reset email sent for active user");
            return RecoveryResult.PasswordReset;
        }

        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, CancellationToken.None);
        if (pending != null)
        {
            // A new secret: the old value is not recoverable from the row by design.
            pending.Secret = _guidFactory.Create();
            pending.SecretHash = ConfirmationSecret.Hash(pending.Secret);
            pending.TokenCreatedUtc = _dateTimeProvider.Now;
            await _registrationRepository.UpdatePending(pending);
            await _activationEmailSender.Send(pending.Email, pending.Secret);

            _logger.LogInformation("Activation email resent for pending registration");
            return RecoveryResult.ActivationResent;
        }

        _logger.LogInformation("Recovery requested for unknown email");
        return RecoveryResult.NotFound;
    }
}
