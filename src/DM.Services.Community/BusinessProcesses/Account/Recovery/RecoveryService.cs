using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset.Confirmation;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.Extensions.Logging;

namespace DM.Services.Community.BusinessProcesses.Account.Recovery;

/// <inheritdoc />
internal class RecoveryService : IRecoveryService
{
    private readonly IUserReadingRepository _userRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly IRegistrationMailSender _activationEmailSender;
    private readonly ITokenFactory _tokenFactory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RecoveryService> _logger;

    public RecoveryService(
        IUserReadingRepository userRepository,
        IRegistrationRepository registrationRepository,
        IPasswordResetRepository passwordResetRepository,
        IPasswordResetEmailSender passwordResetEmailSender,
        IRegistrationMailSender activationEmailSender,
        ITokenFactory tokenFactory,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<RecoveryService> logger)
    {
        _userRepository = userRepository;
        _registrationRepository = registrationRepository;
        _passwordResetRepository = passwordResetRepository;
        _passwordResetEmailSender = passwordResetEmailSender;
        _activationEmailSender = activationEmailSender;
        _tokenFactory = tokenFactory;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RecoveryResult> Recover(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Check if email belongs to an active user
        var user = await _userRepository.GetUserDetailsByEmail(normalizedEmail);
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            // Send password reset email
            var token = _tokenFactory.Create(user.UserId, TokenType.PasswordChange);
            await _passwordResetRepository.ReplacePasswordResetToken(user.UserId, token);
            await _passwordResetEmailSender.Send(user.Email, user.Login, token.TokenId);

            _logger.LogInformation("Password reset email sent for active user. Email={Email}", normalizedEmail);
            return RecoveryResult.PasswordReset;
        }

        // Check if email belongs to a pending registration
        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, CancellationToken.None);
        if (pending != null)
        {
            // Resend activation email with new token
            pending.TokenId = _guidFactory.Create();
            pending.TokenCreatedUtc = _dateTimeProvider.Now;
            await _registrationRepository.UpdatePending(pending);
            await _activationEmailSender.Send(pending.Email, pending.TokenId);

            _logger.LogInformation("Activation email resent for pending registration. Email={Email}", normalizedEmail);
            return RecoveryResult.ActivationResent;
        }

        // Email not found
        _logger.LogInformation("Recovery requested for unknown email. Email={Email}", normalizedEmail);
        return RecoveryResult.NotFound;
    }

    /// <inheritdoc />
    public async Task<EmailAvailabilityResult> CheckEmailAvailability(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Check if email is taken by active user
        var user = await _userRepository.GetUserDetailsByEmail(normalizedEmail);
        if (user != null)
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.Taken);
        }

        // Check if email has pending registration
        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, CancellationToken.None);
        if (pending != null)
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.PendingActivation);
        }

        // Email is available
        return EmailAvailabilityResult.IsAvailable();
    }
}
