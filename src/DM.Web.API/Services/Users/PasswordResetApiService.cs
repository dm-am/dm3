using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Account.PasswordChange;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using FluentValidation;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class PasswordResetApiService : IPasswordResetApiService
{
    private readonly IPasswordResetService passwordResetService;
    private readonly IPasswordChangeService passwordChangeService;
    private readonly IMapper mapper;
    private readonly IValidator<ChangePassword> changePasswordValidator;

    /// <inheritdoc />
    public PasswordResetApiService(
        IPasswordResetService passwordResetService,
        IPasswordChangeService passwordChangeService,
        IMapper mapper,
        IValidator<ChangePassword> changePasswordValidator)
    {
        this.passwordResetService = passwordResetService;
        this.passwordChangeService = passwordChangeService;
        this.mapper = mapper;
        this.changePasswordValidator = changePasswordValidator;
    }

    /// <inheritdoc />
    public async Task Reset(ResetPassword resetPassword)
    {
        var userPasswordReset = mapper.Map<UserPasswordReset>(resetPassword);
        await passwordResetService.Reset(userPasswordReset);
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Change(ChangePassword changePassword)
    {
        await changePasswordValidator.ValidateAndThrowAsync(changePassword);
        var userPasswordChange = mapper.Map<UserPasswordChange>(changePassword);
        var user = await passwordChangeService.Change(userPasswordChange);
        return new Envelope<User>(mapper.Map<User>(user));
    }
}