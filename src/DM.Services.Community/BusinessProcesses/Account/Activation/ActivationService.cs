using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Community.Configuration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <summary>
/// Service for email-first user activation (login selection after email verification)
/// </summary>
internal class ActivationService : IActivationService
{
    private readonly IValidator<ActivationRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IActivationRepository _repository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUserFactory _userFactory;
    private readonly IInvokedEventProducer _producer;
    private readonly IRegistrationMailSender _mailSender;
    private readonly IGuidFactory _guidFactory;
    private readonly TokenConfiguration _tokenConfig;

    public ActivationService(
        IValidator<ActivationRequest> validator,
        IDateTimeProvider dateTimeProvider,
        IActivationRepository repository,
        IRegistrationRepository registrationRepository,
        IUserFactory userFactory,
        IInvokedEventProducer producer,
        IRegistrationMailSender mailSender,
        IGuidFactory guidFactory,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _registrationRepository = registrationRepository;
        _userFactory = userFactory;
        _producer = producer;
        _mailSender = mailSender;
        _guidFactory = guidFactory;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<Guid> Activate(ActivationRequest request)
    {
        // Validate login format and availability
        await _validator.ValidateAndThrowAsync(request);

        var tokenExpiry = TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);

        // Find pending by TokenId (embedded in PendingRegistration)
        var pending = await _repository.FindPendingByToken(request.Token);

        if (pending == null)
        {
            // Check for idempotent retry: maybe already activated?
            if (!string.IsNullOrEmpty(request.ExpectedEmail))
            {
                var existingUser = await _repository.FindUserByEmail(request.ExpectedEmail);
                if (existingUser != null &&
                    string.Equals(existingUser.Login, request.Login, StringComparison.OrdinalIgnoreCase))
                {
                    // Already activated with same login - return success
                    return existingUser.UserId;
                }
            }

            throw new HttpException(HttpStatusCode.Gone, "Ссылка недействительна или уже использована");
        }

        // Check token expiry
        if (pending.TokenCreatedUtc + tokenExpiry < _dateTimeProvider.Now)
        {
            throw new HttpException(HttpStatusCode.Gone, "Ссылка устарела. Запросите новую.");
        }

        // Create User from PendingRegistration
        var user = _userFactory.CreateFromPending(pending, request.Login);

        // Atomic: create user, delete pending
        await _repository.CompleteActivation(user, pending.PendingRegistrationId);

        // Save initial password to history
        await _registrationRepository.SavePasswordToHistory(
            user.UserId,
            user.PasswordHash,
            user.Salt,
            user.PasswordHashVersion,
            CancellationToken.None);

        await _producer.Send(EventType.ActivatedUser, user.UserId);
        return user.UserId;
    }

    /// <inheritdoc />
    public async Task<PendingInfoResult?> GetPendingInfo(Guid tokenId)
    {
        var pending = await _repository.FindPendingByToken(tokenId);

        if (pending == null)
        {
            return null;
        }

        var tokenAge = _dateTimeProvider.Now - pending.TokenCreatedUtc;
        var isExpired = tokenAge > TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);

        return isExpired
            ? PendingInfoResult.Expired(pending.Email)
            : PendingInfoResult.Ready(pending.Email);
    }

    /// <inheritdoc />
    public async Task<bool> ResendActivation(string email)
    {
        // Find pending registration by email
        var pending = await _registrationRepository.FindPendingByEmail(email, CancellationToken.None);

        if (pending == null)
        {
            // Return true anyway to prevent email enumeration
            return true;
        }

        // Update token and send new email
        pending.TokenId = _guidFactory.Create();
        pending.TokenCreatedUtc = _dateTimeProvider.Now;
        await _registrationRepository.UpdatePending(pending);

        await _mailSender.Send(pending.Email, pending.TokenId);
        return true;
    }
}
