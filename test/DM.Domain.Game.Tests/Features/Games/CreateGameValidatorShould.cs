using System;
using DM.Domain.Core.Content;
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
            Title = new string('a', GameFieldLimits.TitleMaxLength + 1),
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('b', GameFieldLimits.InfoMinLength)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    /// <summary>
    /// The bound itself, from the inside: a field filled to the limit the contract
    /// publishes is accepted here.
    /// </summary>
    /// <remarks>
    /// The two layers used to disagree — the contract allowed 200 characters of
    /// title and this validator cut it at 100 — so everything between the numbers
    /// was accepted by the form and refused after the button. Checking only the
    /// "one over the limit" case leaves that gap invisible: it fails either way.
    /// </remarks>
    [Fact]
    public async Task AcceptFieldsFilledToTheLimitTheContractPublishes()
    {
        var input = new CreateGame
        {
            Title = new string('a', GameFieldLimits.TitleMaxLength),
            SystemName = new string('b', GameFieldLimits.SystemMaxLength),
            NarrativeSetting = new string('c', GameFieldLimits.SettingMaxLength),
            Info = new string('d', GameFieldLimits.InfoMinLength)
        };

        var result = await validator.TestValidateAsync(input);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
        result.ShouldNotHaveValidationErrorFor(x => x.SystemName);
        result.ShouldNotHaveValidationErrorFor(x => x.NarrativeSetting);
        result.ShouldNotHaveValidationErrorFor(x => x.Info);
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
            SystemName = new string('a', GameFieldLimits.SystemMaxLength + 1),
            NarrativeSetting = "Forgotten Realms",
            Info = new string('b', GameFieldLimits.InfoMinLength)
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
            NarrativeSetting = new string('a', GameFieldLimits.SettingMaxLength + 1),
            Info = new string('b', GameFieldLimits.InfoMinLength)
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
