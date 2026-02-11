using System;
using System.Threading.Tasks;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset.Confirmation;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.DataAccess.BusinessObjects.Users;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <inheritdoc />
internal class PasswordResetService : IPasswordResetService
{
    private readonly IValidator<UserPasswordReset> _validator;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUserReadingRepository _userReadingRepository;
    private readonly IPasswordResetRepository _repository;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly ILogger<PasswordResetService> _logger;

    /// <inheritdoc />
    public PasswordResetService(
        IValidator<UserPasswordReset> validator,
        ITokenFactory tokenFactory,
        IUserReadingRepository userReadingRepository,
        IPasswordResetRepository repository,
        IPasswordResetEmailSender emailSender,
        ILogger<PasswordResetService> logger)
    {
        _validator = validator;
        _tokenFactory = tokenFactory;
        _userReadingRepository = userReadingRepository;
        _repository = repository;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Reset(UserPasswordReset passwordReset)
    {
        // Validate format only (not existence)
        await _validator.ValidateAndThrowAsync(passwordReset);

        // Silently succeed if user not found — prevents enumeration
        var user = await _userReadingRepository.GetUserDetails(passwordReset.Login);
        if (user == null ||
            user.Email == null ||
            !user.Email.Equals(passwordReset.Email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Password reset requested for non-existent or mismatched login/email. Login={Login}", passwordReset.Login);
            return;
        }

        var token = _tokenFactory.Create(user.UserId, TokenType.PasswordChange);
        await _repository.ReplacePasswordResetToken(user.UserId, token);

        await _emailSender.Send(user.Email, user.Login, token.TokenId);
    }
}