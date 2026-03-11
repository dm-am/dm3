using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
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
            throw new HttpException(System.Net.HttpStatusCode.NotFound, $"User {emailChange.Username} not found");
        }

        var token = _tokenFactory.Create(user.UserId, TokenType.EmailChange);

        await _repository.InvalidateOldEmailChangeTokens(user.UserId);
        await _repository.Update(user.UserId, emailChange.Email, token);

        // Send confirmation to NEW email
        await _mailSender.Send(emailChange.Email, emailChange.Username, token.TokenId);

        // Security: send warning to OLD email about the change request
        // This helps detect account takeover attempts
        if (!string.IsNullOrEmpty(user.Email) && user.Email != emailChange.Email)
        {
            await _warningMailSender.SendAsync(user.Email, emailChange.Username, emailChange.Email);
        }

        await _eventProducer.SendAsync(EventType.EmailChanged, user.UserId);

        return new GeneralUser
        {
            UserId = user.UserId,
            Username = user.Username
        };
    }

    /// <inheritdoc />
    public async Task Confirm(Guid tokenId)
    {
        var foundTokenId = await _confirmationRepository.FindEmailChangeToken(
            tokenId,
            _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.EmailChangeTokenLifetimeHours));

        if (!foundTokenId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound,
                "Email change confirmation token is invalid or expired");
        }

        await _confirmationRepository.MarkTokenUsed(foundTokenId.Value);
    }
}
