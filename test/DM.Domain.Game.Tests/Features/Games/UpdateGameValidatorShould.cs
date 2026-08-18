using System;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class UpdateGameValidatorShould : UnitTestBase
{
    private readonly UpdateGameValidator validator;

    public UpdateGameValidatorShould()
    {
        validator = new UpdateGameValidator();
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            Title = "Updated Title"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenTitleIsEmptyString()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            Title = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenSystemNameIsEmptyString()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            SystemName = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenSystemNameExceedsMaxLength()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            SystemName = new string('a', 51)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingIsEmptyString()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            NarrativeSetting = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingExceedsMaxLength()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            NarrativeSetting = new string('a', 51)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenInfoIsTooShort()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            Info = new string('a', 199)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Info)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public async Task PassWhenOptionalFieldsAreNull()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid()
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
