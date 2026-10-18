using System;
using DM.Domain.Core.Tokens;
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
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TokenConfiguration _tokenConfig;

    /// <inheritdoc />
    public EmailChangeService(
        IValidator<UserEmailChange> validator,
        ITokenFactory tokenFactory,
        IEmailChangeRepository repository,
        IEmailChangeConfirmationRepository confirmationRepository,
        IEmailChangeMailSender mailSender,
        IEmailChangeNoticeMailSender noticeMailSender,
        IEventProducer eventProducer,
        ISecurityAuditRepository auditService,
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
        _auditService = auditService;
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
            throw new HttpException(System.Net.HttpStatusCode.NotFound,
                RefusalMessage.UserNotFoundByUsername(emailChange.Username));
        }

        var token = _tokenFactory.Create(user.UserId, TokenType.EmailChange);

        // One call, because retiring the previous letters and issuing this one is
        // one decision: as two, a request from another tab slipped between them.
        await _repository.RequestChange(user.UserId, emailChange.Email, token);

        // Send confirmation to NEW email
        // Secret, not TokenId — see ConfirmationSecret.
        await _mailSender.Send(emailChange.Email, token.Secret);

        // Security: send warning to OLD email about the change request
        // This helps detect account takeover attempts
        if (!string.IsNullOrEmpty(user.Email) && user.Email != emailChange.Email)
        {
            await _noticeMailSender.SendRequested(user.Email, emailChange.Email);
        }

        // The security journal, alongside the warning letter and for the same
        // reason: every recovery path in the product goes through the address on
        // the account, so its change is the first thing a hijacked account shows.
        await _auditService.LogAsync(user.UserId, SecurityEventType.EmailChange);

        await _eventProducer.SendAsync(EventType.EmailChanged, user.UserId);

        return new GeneralUser
        {
            UserId = user.UserId,
            Username = user.Username
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is where the address actually moves. It used to move on the request,
    /// which left this method marking a token used and nothing else: the letter
    /// confirmed a change that had already happened, and a mistyped address took
    /// the account away from its owner before they could read the letter saying
    /// so.
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
            // The address was free when the change was asked for and somebody
            // took it while the letter waited in the mailbox. The answer is the
            // one the form gives for an address in use, under the same field, so
            // that the two paths do not describe one situation two ways.
            //
            // The link is spent all the same: it authorised a move that cannot
            // happen, and leaving it live would only let the same refusal be
            // collected again. Requesting another address issues another letter.
            await _confirmationRepository.MarkTokenUsed(secret);
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(UserEmailChange.Email)] = ValidationError.Taken
            });
        }

        // A live token with nothing pending is a link followed twice, or one
        // whose request was withdrawn. Same answer as an expired link: there is
        // nothing here to confirm.
        if (moved is null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        await _confirmationRepository.MarkTokenUsed(secret);

        // The second letter of the pair, to the address the account just left.
        // The first one could still be stopped by a new password; this one only
        // tells, and it is the whole reason the reader who missed the first hears
        // about the move at all.
        await _noticeMailSender.SendCompleted(moved.PreviousEmail, moved.NewEmail);
    }
}
