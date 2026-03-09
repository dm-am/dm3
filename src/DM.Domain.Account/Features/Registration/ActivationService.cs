using System;
using DM.Domain.Core.Abstractions;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Service for email-first user activation (username selection after email verification)
/// </summary>
internal class ActivationService : IActivationService
{
    private readonly IValidator<ActivationRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IActivationRepository _repository;
    private readonly IUserFactory _userFactory;
    private readonly IEventProducer _producer;
    private readonly IRecoveryService _recoveryService;
    private readonly TokenConfiguration _tokenConfig;

    public ActivationService(
        IValidator<ActivationRequest> validator,
        IDateTimeProvider dateTimeProvider,
        IActivationRepository repository,
        IUserFactory userFactory,
        IEventProducer producer,
        IRecoveryService recoveryService,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _userFactory = userFactory;
        _producer = producer;
        _recoveryService = recoveryService;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<Guid> Activate(ActivationRequest request)
    {
        // Validate username format and availability
        await _validator.ValidateAndThrowAsync(request);

        var tokenExpiry = TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);

        // Find pending by TokenId (embedded in PendingRegistration)
        var pending = await _repository.FindPendingByToken(request.Token);

        if (pending == null)
        {
            // Check for idempotent retry: maybe already activated?
            if (!string.IsNullOrEmpty(request.RetryEmail))
            {
                var existingUser = await _repository.FindUserByEmail(request.RetryEmail);
                if (existingUser != null &&
                    string.Equals(existingUser.Username, request.Username, StringComparison.OrdinalIgnoreCase))
                {
                    // Already activated with same username - return success
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
        var user = _userFactory.CreateFromPending(pending, request.Username);

        // Atomic: create user, delete pending
        await _repository.CompleteActivation(user, pending.PendingRegistrationId);

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
        // Delegate to unified recovery service which handles activation resend
        var result = await _recoveryService.Recover(email);
        return result == RecoveryResult.ActivationResent;
    }
}
