using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc />
internal class EmailChangeService : IEmailChangeService
{
    private readonly IValidator<UserEmailChange> _validator;
    private readonly ITokenFactory _tokenFactory;
    private readonly IEmailChangeRepository _repository;
    private readonly IEmailChangeConfirmationRepository _confirmationRepository;
    private readonly IEmailChangeMailSender _mailSender;
    private readonly IEmailChangeNoticeMailSender _noticeMailSender;
    private readonly IEventProducer _eventProducer;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TokenConfiguration _tokenConfig;

    public EmailChangeService(
        IValidator<UserEmailChange> validator,
        ITokenFactory tokenFactory,
        IEmailChangeRepository repository,
        IEmailChangeConfirmationRepository confirmationRepository,
        IEmailChangeMailSender mailSender,
        IEmailChangeNoticeMailSender noticeMailSender,
        IEventProducer eventProducer,
        ISecurityAuditRepository auditRepository,
        IDateTimeProvider dateTimeProvider,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _tokenFactory = tokenFactory;
        _repository = repository;
        _confirmationRepository = confirmationRepository;
        _mailSender = mailSender;
        _noticeMailSender = noticeMailSender;
        _eventProducer = eventProducer;
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserEmailChange emailChange)
    {
        await _validator.ValidateAndThrowAsync(emailChange);
        var user = await _repository.FindUser(emailChange.Username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound,
                RefusalMessage.UserNotFoundByUsername(emailChange.Username));
        }

        var token = _tokenFactory.Create(user.UserId, TokenType.EmailChange);

        // One call, because retiring the previous letters and issuing this one is
        // one decision: as two, a request from another tab slipped between them.
        await _repository.RequestChange(user.UserId, emailChange.Email, token);

        // Secret, not TokenId — see ConfirmationSecret.
        await _mailSender.Send(emailChange.Email, token.Secret);

        // The warning to the old address shows an account takeover early
        if (!string.IsNullOrEmpty(user.Email) && user.Email != emailChange.Email)
        {
            await _noticeMailSender.SendRequested(user.Email, emailChange.Email);
        }

        // Journaled for the same reason as the warning letter.
        await _auditRepository.LogAsync(user.UserId, SecurityEventType.EmailChange);

        await _eventProducer.SendAsync(EventType.EmailChanged, user.UserId);

        return new GeneralUser
        {
            UserId = user.UserId,
            Username = user.Username
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// The address moves here, not on the request, so a mistyped address cannot take
    /// the account away before its owner reads the letter.
    /// </remarks>
    public async Task Confirm(Guid secret)
    {
        var ownerId = await _confirmationRepository.FindEmailChangeTokenOwner(
            secret,
            _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.EmailChangeTokenLifetimeHours));

        if (!ownerId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        EmailMove? moved;
        try
        {
            moved = await _repository.ApplyPendingEmail(ownerId.Value);
        }
        catch (DuplicateEntityException)
        {
            // Taken while the letter waited: the form's answer for an address in use,
            // and the link is spent all the same.
            await _confirmationRepository.MarkTokenUsed(secret);
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(UserEmailChange.Email)] = ValidationError.Taken
            });
        }

        // A link followed twice or a withdrawn request: nothing to confirm.
        if (moved is null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        await _confirmationRepository.MarkTokenUsed(secret);

        // The second letter of the pair, to the address the account just left.
        await _noticeMailSender.SendCompleted(moved.PreviousEmail, moved.NewEmail);
    }
}
