using DM.Domain.Core.Tokens;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
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
        await _compromisedPasswordChecker.ThrowIfCompromised(registration.Password, nameof(registration.Password));

        var (hash, salt) = _securityManager.GeneratePassword(registration.Password);
        var now = _dateTimeProvider.Now;

        // The letter carries the secret, the row its hash (see ConfirmationSecret).
        // The row identifier is drawn first: a deterministic IGuidFactory reads the order.
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

        // One write that replaces any pending row, so two submissions cannot race.
        await _repository.AddPending(pending);

        await _mailSender.Send(registration.Email, pending.Secret);
    }
}
