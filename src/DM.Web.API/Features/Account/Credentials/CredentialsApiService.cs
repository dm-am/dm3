using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.UsernameChange;
using DM.Web.API.Features.Community.Users;
using ServiceCreateUsernameChangeRequest = DM.Domain.Account.Features.UsernameChange.CreateUsernameChangeRequest;

namespace DM.Web.API.Features.Account.Credentials;

/// <inheritdoc />
internal class CredentialsApiService : ICredentialsApiService
{
    private readonly IPasswordChangeService _passwordChangeService;
    private readonly IEmailChangeService _emailChangeService;
    private readonly IUsernameChangeService _usernameChangeService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <summary>
    /// Creates a new instance of CredentialsApiService
    /// </summary>
    public CredentialsApiService(
        IPasswordChangeService passwordChangeService,
        IEmailChangeService emailChangeService,
        IUsernameChangeService usernameChangeService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _passwordChangeService = passwordChangeService;
        _emailChangeService = emailChangeService;
        _usernameChangeService = usernameChangeService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<User> ChangePassword(PasswordChangeRequest request)
    {
        var passwordChange = new UserPasswordChange
        {
            OldPassword = request.OldPassword,
            NewPassword = request.NewPassword
        };
        var user = await _passwordChangeService.Change(passwordChange);
        return _mapper.Map<User>(user);
    }

    /// <inheritdoc />
    public async Task<User> RequestEmailChange(EmailChangeRequest request)
    {
        var emailChange = new UserEmailChange
        {
            Username = _identityProvider.Current.User.Username,
            Password = request.Password,
            Email = request.Email
        };
        var user = await _emailChangeService.Change(emailChange);
        return _mapper.Map<User>(user);
    }

    /// <inheritdoc />
    public Task ConfirmEmailChange(Guid token) => _emailChangeService.Confirm(token);

    /// <inheritdoc />
    public async Task<UsernameChangeResponse> RequestUsernameChange(UsernameChangeCreateRequest request)
    {
        var serviceRequest = _mapper.Map<ServiceCreateUsernameChangeRequest>(request);
        var result = await _usernameChangeService.Create(serviceRequest);
        return _mapper.Map<UsernameChangeResponse>(result);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeResponse?> GetUsernameChangeStatus()
    {
        var result = await _usernameChangeService.GetCurrentUserRequest();
        return result != null
            ? _mapper.Map<UsernameChangeResponse>(result)
            : null;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeResponse?> GetUsernameChangeApproval(Guid token)
    {
        var result = await _usernameChangeService.GetByApprovalToken(token);
        return result != null
            ? _mapper.Map<UsernameChangeResponse>(result)
            : null;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeResponse> CompleteUsernameChange(Guid token, UsernameChangeCompletionRequest request)
    {
        var result = await _usernameChangeService.CompleteWithToken(token, request.Username);
        return _mapper.Map<UsernameChangeResponse>(result);
    }
}
