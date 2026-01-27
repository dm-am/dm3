using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Community.BusinessProcesses.Account.Activation;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <inheritdoc />
internal class RegistrationService : IRegistrationService
{
    private readonly IValidator<UserRegistration> _validator;
    private readonly ISecurityManager _securityManager;
    private readonly IUserFactory _userFactory;
    private readonly IActivationTokenFactory _activationTokenFactory;
    private readonly IRegistrationRepository _repository;
    private readonly IRegistrationMailSender _mailSender;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public RegistrationService(
        IValidator<UserRegistration> validator,
        ISecurityManager securityManager,
        IUserFactory userFactory,
        IActivationTokenFactory activationTokenFactory,
        IRegistrationRepository repository,
        IRegistrationMailSender mailSender,
        IInvokedEventProducer producer)
    {
        _validator = validator;
        _securityManager = securityManager;
        _userFactory = userFactory;
        _activationTokenFactory = activationTokenFactory;
        _repository = repository;
        _mailSender = mailSender;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task Register(UserRegistration registration)
    {
        await _validator.ValidateAndThrowAsync(registration);

        var (hash, salt, version) = _securityManager.GeneratePassword(registration.Password);
        var user = _userFactory.Create(registration, salt, hash, version);
        var token = _activationTokenFactory.Create(user.UserId);

        await _repository.AddUser(user, token);
        await _mailSender.Send(user.Email, user.Login, token.TokenId);
        await _producer.Send(EventType.NewUser, user.UserId);
    }
}