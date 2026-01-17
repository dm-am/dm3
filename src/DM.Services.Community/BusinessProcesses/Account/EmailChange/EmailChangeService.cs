using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.Activation;
using DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange;

/// <inheritdoc />
internal class EmailChangeService : IEmailChangeService
{
    private readonly IValidator<UserEmailChange> _validator;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IActivationTokenFactory _tokenFactory;
    private readonly IEmailChangeRepository _repository;
    private readonly IEmailChangeMailSender _mailSender;

    /// <inheritdoc />
    public EmailChangeService(
        IValidator<UserEmailChange> validator,
        IUpdateBuilderFactory updateBuilderFactory,
        IActivationTokenFactory tokenFactory,
        IEmailChangeRepository repository,
        IEmailChangeMailSender mailSender)
    {
        _validator = validator;
        _updateBuilderFactory = updateBuilderFactory;
        _tokenFactory = tokenFactory;
        _repository = repository;
        _mailSender = mailSender;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserEmailChange emailChange)
    {
        await _validator.ValidateAndThrowAsync(emailChange);
        var user = await _repository.FindUser(emailChange.Login);

        var updateUser = _updateBuilderFactory.Create<User>(user.UserId)
            .Field(u => u.Email, emailChange.Email)
            .Field(u => u.Activated, false);
        var token = _tokenFactory.Create(user.UserId);

        await _repository.Update(updateUser, token);
        await _mailSender.Send(emailChange.Email, emailChange.Login, token.TokenId);

        return user;
    }
}