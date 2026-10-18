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

/// <summary>
/// Service for email-first user activation (username selection after email verification)
/// </summary>
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
        // The link first, the name second. The other order let anybody probe a
        // name with a made-up token: a taken name came back as 400 and a free
        // one as 410, with nothing written down. A request without a live link
        // now learns one thing, that the link is dead, whatever name it carries.
        //
        // The retry by address that used to live in the dead-link branch is
        // gone with it. It handed out the account whose address and name were
        // supplied, on no proof but those two, and only the leak above stood in
        // its way: with the name checked first, an existing name never reached
        // it. A click that lands twice is still answered - by RefuseOrRepeat,
        // after a link that was found.
        var pending = await _repository.FindPendingByToken(request.Token);
        if (pending == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.LinkInvalidOrUsed);
        }

        var tokenExpiry = TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);
        if (pending.TokenCreatedUtc + tokenExpiry < _dateTimeProvider.Now)
        {
            throw new HttpException(HttpStatusCode.Gone, "Ссылка устарела. Запросите новую.");
        }

        // Format and availability of the name, for the holder of a live link.
        await _validator.ValidateAndThrowAsync(request);

        // Create User from PendingRegistration
        var user = _userFactory.CreateFromPending(pending, request.Username);

        // The zone of the device the account is created on, kept for the letters,
        // which have no browser to ask. One nobody can print a time in is left out
        // rather than refused: the account keeps UTC and its owner can set another.
        user.TimeZoneId = ReaderTime.IsKnownZone(request.TimeZoneId) ? request.TimeZoneId!.Trim() : null;

        // Atomic: create user, delete pending
        try
        {
            await _repository.CompleteActivation(user, pending.PendingRegistrationId);
        }
        catch (DuplicateEntityException)
        {
            // The validator found the name free and the address free, and one of
            // them stopped being so before this write. Which one is established by
            // reading rather than by the name of the constraint, because the same
            // collision means three different things to three different people.
            return await RefuseOrRepeat(pending.Email, request.Username);
        }

        await _producer.SendAsync(EventType.ActivatedUser, user.UserId);
        return user.UserId;
    }

    /// <summary>
    /// The account the address already belongs to, when that account is this very
    /// activation done once already.
    /// </summary>
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

        var tokenAge = _dateTimeProvider.Now - pending.TokenCreatedUtc;
        var isExpired = tokenAge > TimeSpan.FromHours(_tokenConfig.ActivationTokenLifetimeHours);

        return isExpired
            ? PendingInfoResult.Expired(pending.Email)
            : PendingInfoResult.Ready(pending.Email);
    }
}
