using System;
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
    private readonly IEmailChangeWarningMailSender _warningMailSender;
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
        IEmailChangeWarningMailSender warningMailSender,
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
        _warningMailSender = warningMailSender;
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

        await _repository.InvalidateOldEmailChangeTokens(user.UserId);
        await _repository.RequestChange(user.UserId, emailChange.Email, token);

        // Send confirmation to NEW email
        await _mailSender.Send(emailChange.Email, emailChange.Username, token.TokenId);

        // Security: send warning to OLD email about the change request
        // This helps detect account takeover attempts
        if (!string.IsNullOrEmpty(user.Email) && user.Email != emailChange.Email)
        {
            await _warningMailSender.SendAsync(user.Email, emailChange.Username, emailChange.Email);
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
    /// <inheritdoc />
    /// <remarks>
    /// This is where the address actually moves. It used to move on the request,
    /// which left this method marking a token used and nothing else: the letter
    /// confirmed a change that had already happened, and a mistyped address took
    /// the account away from its owner before they could read the letter saying
    /// so.
    /// </remarks>
    public async Task Confirm(Guid tokenId)
    {
        var ownerId = await _confirmationRepository.FindEmailChangeTokenOwner(
            tokenId,
            _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.EmailChangeTokenLifetimeHours));

        if (!ownerId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        // A live token with nothing pending is a link followed twice, or one
        // whose request was withdrawn. Same answer as an expired link: there is
        // nothing here to confirm.
        if (!await _repository.ApplyPendingEmail(ownerId.Value))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
        }

        await _confirmationRepository.MarkTokenUsed(tokenId);
    }
}
