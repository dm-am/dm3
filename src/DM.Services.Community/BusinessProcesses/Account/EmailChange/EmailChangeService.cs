using System.Threading.Tasks;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Core.Exceptions;
using DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange;

/// <inheritdoc />
internal class EmailChangeService : IEmailChangeService
{
    private readonly IValidator<UserEmailChange> _validator;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ITokenFactory _tokenFactory;
    private readonly IEmailChangeRepository _repository;
    private readonly IEmailChangeMailSender _mailSender;
    private readonly IInvokedEventProducer _eventProducer;

    /// <inheritdoc />
    public EmailChangeService(
        IValidator<UserEmailChange> validator,
        IUpdateBuilderFactory updateBuilderFactory,
        ITokenFactory tokenFactory,
        IEmailChangeRepository repository,
        IEmailChangeMailSender mailSender,
        IInvokedEventProducer eventProducer)
    {
        _validator = validator;
        _updateBuilderFactory = updateBuilderFactory;
        _tokenFactory = tokenFactory;
        _repository = repository;
        _mailSender = mailSender;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserEmailChange emailChange)
    {
        await _validator.ValidateAndThrowAsync(emailChange);
        var user = await _repository.FindUser(emailChange.Login);
        if (user == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, $"User {emailChange.Login} not found");
        }

        var updateUser = _updateBuilderFactory.Create<User>(user.UserId)
            .Field(u => u.Email, emailChange.Email);
        var token = _tokenFactory.Create(user.UserId, TokenType.EmailChange);

        await _repository.InvalidateOldEmailChangeTokens(user.UserId);
        await _repository.Update(updateUser, token);
        await _mailSender.Send(emailChange.Email, emailChange.Login, token.TokenId);

        // Audit logging: record email change event
        await _eventProducer.Send(EventType.EmailChanged, user.UserId);

        return user;
    }
}