using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class CreateGameValidatorShould : UnitTestBase
{
    private readonly CreateGameValidator validator;
    private readonly Mock<IUserLookupService> userLookupServiceMock;

    public CreateGameValidatorShould()
    {
        userLookupServiceMock = Mock<IUserLookupService>();
        validator = new CreateGameValidator(userLookupServiceMock.Object);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new CreateGame
        {
            Title = "Valid Game Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenTitleIsEmpty()
    {
        var input = new CreateGame
        {
            Title = "",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateGame
        {
            Title = new string('a', 101),
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('b', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenSystemNameIsEmpty()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenSystemNameExceedsMaxLength()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = new string('a', 51),
            NarrativeSetting = "Forgotten Realms",
            Info = new string('b', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingIsEmpty()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "",
            Info = new string('a', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingExceedsMaxLength()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = new string('a', 51),
            Info = new string('b', 200)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenInfoIsEmpty()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Info)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenInfoIsTooShort()
    {
        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 199)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Info)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public async Task FailWhenAssistantUsernameDoesNotExist()
    {
        userLookupServiceMock
            .Setup(s => s.UsernameExistsAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 200),
            AssistantUsername = "nonexistent"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.AssistantUsername)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task PassWhenAssistantUsernameExists()
    {
        userLookupServiceMock
            .Setup(s => s.UsernameExistsAsync("validassistant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new CreateGame
        {
            Title = "Valid Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', 200),
            AssistantUsername = "validassistant"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
