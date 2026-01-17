using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset.Confirmation;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <inheritdoc />
internal class PasswordResetService : IPasswordResetService
{
    private readonly IValidator<UserPasswordReset> _validator;
    private readonly IPasswordResetTokenFactory _tokenFactory;
    private readonly IUserReadingRepository _userReadingRepository;
    private readonly IPasswordResetRepository _repository;
    private readonly IPasswordResetEmailSender _emailSender;

    /// <inheritdoc />
    public PasswordResetService(
        IValidator<UserPasswordReset> validator,
        IPasswordResetTokenFactory tokenFactory,
        IUserReadingRepository userReadingRepository,
        IPasswordResetRepository repository,
        IPasswordResetEmailSender emailSender)
    {
        _validator = validator;
        _tokenFactory = tokenFactory;
        _userReadingRepository = userReadingRepository;
        _repository = repository;
        _emailSender = emailSender;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Reset(UserPasswordReset passwordReset)
    {
        await _validator.ValidateAndThrowAsync(passwordReset);
        var user = await _userReadingRepository.GetUserDetails(passwordReset.Login);

        var token = _tokenFactory.Create(user.UserId);
        await _repository.CreateToken(token);

        await _emailSender.Send(user.Email, user.Login, token.TokenId);
        return user;
    }
}