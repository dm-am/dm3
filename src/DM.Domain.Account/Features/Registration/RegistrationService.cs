using System.Collections.Generic;
using DM.Domain.Core.Tokens;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Service for email-first user registration.
/// Creates PendingRegistration; User is created later during activation when username is chosen.
/// </summary>
internal class RegistrationService : IRegistrationService
{
    private readonly IValidator<UserRegistration> _validator;
    private readonly ISecurityManager _securityManager;
    private readonly ICompromisedPasswordChecker _compromisedPasswordChecker;
    private readonly IRegistrationRepository _repository;
    private readonly IRegistrationMailSender _mailSender;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegistrationService(
        IValidator<UserRegistration> validator,
        ISecurityManager securityManager,
        ICompromisedPasswordChecker compromisedPasswordChecker,
        IRegistrationRepository repository,
        IRegistrationMailSender mailSender,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _validator = validator;
        _securityManager = securityManager;
        _compromisedPasswordChecker = compromisedPasswordChecker;
        _repository = repository;
        _mailSender = mailSender;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task Register(UserRegistration registration)
    {
        await _validator.ValidateAndThrowAsync(registration);

        // NIST SP 800-63B: Check if password has been compromised in data breaches
        if (await _compromisedPasswordChecker.IsCompromisedAsync(registration.Password))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(registration.Password)] = RefusalMessage.PasswordBreached
            });
        }

        var (hash, salt) = _securityManager.GeneratePassword(registration.Password);
        var now = _dateTimeProvider.Now;

        // The letter carries the secret; the row keeps its hash — see
        // ConfirmationSecret for why a confirmation link is stored like a password.
        // The row identifier is drawn first, keeping the order the deterministic
        // factory of the seeding tool is read in.
        var pendingId = _guidFactory.Create();
        var secret = _guidFactory.Create();
        var pending = new PendingRegistration
        {
            PendingRegistrationId = pendingId,
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            Email = registration.Email.ToLowerInvariant(),
            PasswordHash = hash,
            Salt = salt,
            CreatedUtc = now,
            TokenCreatedUtc = now,
            AcceptedRules = registration.AcceptedRules
        };

        // If pending already exists for this email, replace it (re-registration with possibly new password)
        if (await _repository.PendingExists(registration.Email, CancellationToken.None))
        {
            await _repository.ReplacePending(pending);
        }
        else
        {
            await _repository.AddPending(pending);
        }

        // Send confirmation email (no username yet)
        await _mailSender.Send(registration.Email, pending.Secret);
    }
}
