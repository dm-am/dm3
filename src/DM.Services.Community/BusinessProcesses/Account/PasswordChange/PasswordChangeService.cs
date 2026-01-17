using System.Threading.Tasks;
using DM.Services.Authentication.Implementation;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange;

/// <inheritdoc />
internal class PasswordChangeService : IPasswordChangeService
{
    private readonly IValidator<UserPasswordChange> _validator;
    private readonly IPasswordChangeRepository _repository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;

    /// <inheritdoc />
    public PasswordChangeService(
        IValidator<UserPasswordChange> validator,
        ISecurityManager securityManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IPasswordChangeRepository repository,
        IAuthenticationService authenticationService,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _repository = repository;
        _authenticationService = authenticationService;
        _identityProvider = identityProvider;
        _securityManager = securityManager;
        _updateBuilderFactory = updateBuilderFactory;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Change(UserPasswordChange passwordChange)
    {
        await _validator.ValidateAndThrowAsync(passwordChange);
        var user = passwordChange.Token.HasValue
            ? await _repository.FindUser(passwordChange.Token.Value)
            : _identityProvider.Current.User;

        var (hash, salt) = _securityManager.GeneratePassword(passwordChange.NewPassword);
        var userUpdate = _updateBuilderFactory.Create<User>(user.UserId)
            .Field(u => u.PasswordHash, hash)
            .Field(u => u.Salt, salt);
        var tokenUpdate = passwordChange.Token.HasValue
            ? _updateBuilderFactory.Create<Token>(passwordChange.Token.Value)
                .Field(t => t.IsRemoved, true)
            : null;

        await _repository.UpdatePassword(userUpdate, tokenUpdate);
        await _authenticationService.LogoutElsewhere();

        return user;
    }
}