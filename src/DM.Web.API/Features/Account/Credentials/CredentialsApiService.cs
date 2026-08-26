using System;
using System.Threading.Tasks;
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
    private readonly CredentialsMapper _mapper;
    private readonly UserMapper _userMapper;

    /// <summary>
    /// Creates a new instance of CredentialsApiService
    /// </summary>
    public CredentialsApiService(
        IPasswordChangeService passwordChangeService,
        IEmailChangeService emailChangeService,
        IUsernameChangeService usernameChangeService,
        IIdentityProvider identityProvider,
        CredentialsMapper mapper,
        UserMapper userMapper)
    {
        _passwordChangeService = passwordChangeService;
        _emailChangeService = emailChangeService;
        _usernameChangeService = usernameChangeService;
        _identityProvider = identityProvider;
        _mapper = mapper;
        _userMapper = userMapper;
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
        return _userMapper.ToUser(user);
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
        return _userMapper.ToUser(user);
    }

    /// <inheritdoc />
    public Task ConfirmEmailChange(Guid token) => _emailChangeService.Confirm(token);

    /// <inheritdoc />
    public async Task<UsernameChangeResponse> RequestUsernameChangeAsync(UsernameChangeCreateRequest request)
    {
        var serviceRequest = _mapper.ToCreateRequest(request);
        var result = await _usernameChangeService.CreateAsync(serviceRequest);
        return _mapper.ToResponse(result);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeResponse?> GetUsernameChangeStatusAsync()
    {
        var result = await _usernameChangeService.GetCurrentUserRequestAsync();
        return result != null
            ? _mapper.ToResponse(result)
            : null;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeApprovalInfo?> GetUsernameChangeApprovalAsync(Guid token)
    {
        var result = await _usernameChangeService.GetApprovalInfoAsync(token);
        return result == null
            ? null
            : new UsernameChangeApprovalInfo
            {
                Status = result.Status,
                CurrentUsername = result.CurrentUsername
            };
    }

    /// <inheritdoc />
    public async Task<UsernameChangeResponse> CompleteUsernameChangeAsync(Guid token, UsernameChangeCompletionRequest request)
    {
        var result = await _usernameChangeService.CompleteWithTokenAsync(token, request.Username);
        return _mapper.ToResponse(result);
    }
}
