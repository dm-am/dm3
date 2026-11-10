using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Dates;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Registration;

internal class ActivationService : IActivationService
{
    private readonly IValidator<ActivationRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IActivationRepository _repository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUserFactory _userFactory;
    private readonly IEventProducer _producer;
    private readonly TokenConfiguration _tokenConfig;

    public ActivationService(
        IValidator<ActivationRequest> validator,
        IDateTimeProvider dateTimeProvider,
        IActivationRepository repository,
        IRegistrationRepository registrationRepository,
        IUserFactory userFactory,
        IEventProducer producer,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _registrationRepository = registrationRepository;
        _userFactory = userFactory;
        _producer = producer;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<Guid> Activate(ActivationRequest request)
    {
        // The link first, the name second, so a made-up token cannot probe names.
        var pending = await _repository.FindPendingByToken(request.Token);
        if (pending == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.LinkInvalidOrUsed);
        }

        if (IsExpired(pending))
        {
            throw new HttpException(HttpStatusCode.Gone, "Ссылка устарела. Запросите новую.");
        }

        // Format and availability of the name, for the holder of a live link.
        await _validator.ValidateAndThrowAsync(request);

        var user = _userFactory.CreateFromPending(pending, request.Username);

        // The zone of the device the account is created on, kept for the letters,
        // which have no browser to ask. One nobody can print a time in is left out
        // rather than refused: the account keeps UTC and its owner can set another.
        user.TimeZoneId = ReaderTime.IsKnownZone(request.TimeZoneId) ? request.TimeZoneId!.Trim() : null;

        try
        {
            await _repository.CompleteActivation(user, pending.PendingRegistrationId);
        }
        catch (DuplicateEntityException)
        {
            // The name or the address stopped being free after the validator.
            return await RefuseOrRepeat(pending.Email, request.Username);
        }

        await _producer.SendAsync(EventType.ActivatedUser, user.UserId);
        return user.UserId;
    }

    // The account the address already belongs to, when that account is this very activation done
    // once already.
    private async Task<Guid?> AlreadyActivatedAs(string email, string username)
    {
        var existingUser = await _repository.FindUserByEmail(email);
        return existingUser != null &&
               string.Equals(existingUser.Username, username, StringComparison.OrdinalIgnoreCase)
            ? existingUser.UserId
            : null;
    }

    /// <summary>
    /// What a collision at the moment of writing the account turns out to be.
    /// </summary>
    /// <remarks>
    /// Three outcomes, and the person on the other side needs to be told which:
    /// the same click landing twice, which is a success and must not read as a
    /// failure; a name somebody else took in the meantime, which is answered
    /// under the name field so the form can offer another one; and the address
    /// itself gone to another account, which leaves this registration with
    /// nothing to complete.
    /// </remarks>
    private async Task<Guid> RefuseOrRepeat(string email, string username)
    {
        var repeated = await AlreadyActivatedAs(email, username);
        if (repeated.HasValue)
        {
            return repeated.Value;
        }

        if (!await _registrationRepository.UsernameFree(username, CancellationToken.None))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(ActivationRequest.Username)] = ValidationError.Taken
            });
        }

        throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.LinkInvalidOrUsed);
    }

    /// <inheritdoc />
    public async Task<PendingInfoResult?> GetPendingInfo(Guid tokenId)
    {
        var pending = await _repository.FindPendingByToken(tokenId);

        if (pending == null)
        {
            return null;
        }

        return IsExpired(pending)
            ? PendingInfoResult.Expired(pending.Email)
            : PendingInfoResult.Ready(pending.Email);
    }

    private bool IsExpired(PendingRegistration pending) =>
        _dateTimeProvider.Now - pending.TokenCreatedUtc > TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);
}
