using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Blacklists;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Blacklists;

public class OperateBlacklistLinkValidatorShould : UnitTestBase
{
    private readonly OperateBlacklistLinkValidator validator;
    private readonly IUserLookupService userLookupService;

    public OperateBlacklistLinkValidatorShould()
    {
        userLookupService = Mock<IUserLookupService>();
        validator = new OperateBlacklistLinkValidator(userLookupService);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        userLookupService
            .UsernameExistsAsync("existinguser", Arg.Any<CancellationToken>()).Returns(true);

        var input = new OperateBlacklistLink
        {
            GameId = Guid.NewGuid(),
            Username = "existinguser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenGameIdIsEmpty()
    {
        var input = new OperateBlacklistLink
        {
            GameId = Guid.Empty,
            Username = "validuser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameIsEmpty()
    {
        var input = new OperateBlacklistLink
        {
            GameId = Guid.NewGuid(),
            Username = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameIsNull()
    {
        var input = new OperateBlacklistLink
        {
            GameId = Guid.NewGuid(),
            Username = null!
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public async Task FailWhenUserDoesNotExist()
    {
        userLookupService
            .UsernameExistsAsync("nonexistentuser", Arg.Any<CancellationToken>()).Returns(false);

        var input = new OperateBlacklistLink
        {
            GameId = Guid.NewGuid(),
            Username = "nonexistentuser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
