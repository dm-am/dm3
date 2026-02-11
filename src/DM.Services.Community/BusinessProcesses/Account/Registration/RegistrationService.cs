using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// Service for email-first user registration.
/// Creates PendingRegistration; User is created later during activation when login is chosen.
/// </summary>
internal class RegistrationService : IRegistrationService
{
    private readonly IValidator<UserRegistration> _validator;
    private readonly ISecurityManager _securityManager;
    private readonly IRegistrationRepository _repository;
    private readonly IRegistrationMailSender _mailSender;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegistrationService(
        IValidator<UserRegistration> validator,
        ISecurityManager securityManager,
        IRegistrationRepository repository,
        IRegistrationMailSender mailSender,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _validator = validator;
        _securityManager = securityManager;
        _repository = repository;
        _mailSender = mailSender;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task Register(UserRegistration registration)
    {
        await _validator.ValidateAndThrowAsync(registration);

        var (hash, salt, version) = _securityManager.GeneratePassword(registration.Password);
        var now = _dateTimeProvider.Now;

        // Create PendingRegistration with embedded TokenId
        var pending = new PendingRegistration
        {
            PendingRegistrationId = _guidFactory.Create(),
            TokenId = _guidFactory.Create(),
            Email = registration.Email.ToLowerInvariant(),
            PasswordHash = hash,
            Salt = salt,
            PasswordHashVersion = version,
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

        // Send confirmation email (no login yet)
        await _mailSender.Send(registration.Email, pending.TokenId);
    }
}
