using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Account.EmailChange;
using DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class EmailChangeApiService : IEmailChangeApiService
{
    private readonly IEmailChangeService _service;
    private readonly IEmailChangeConfirmationService _confirmationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public EmailChangeApiService(
        IEmailChangeService service,
        IEmailChangeConfirmationService confirmationService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _service = service;
        _confirmationService = confirmationService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Change(ChangeEmail changeEmail)
    {
        var emailChange = new UserEmailChange
        {
            Login = _identityProvider.Current.User.Login,
            Password = changeEmail.Password,
            Email = changeEmail.Email
        };
        var user = await _service.Change(emailChange);
        return new Envelope<User>(_mapper.Map<User>(user));
    }

    /// <inheritdoc />
    public Task ConfirmEmailChange(Guid token) => _confirmationService.Confirm(token);
}