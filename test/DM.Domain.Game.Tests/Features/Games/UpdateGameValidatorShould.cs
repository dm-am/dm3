using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class UpdateGameValidatorShould
{
    private readonly UpdateGameValidator validator = new();

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
    public async Task FailWhenCommentsAccessModeIsNotInEnum()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            CommentsAccessMode = (CommentsAccessMode)30000
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.CommentsAccessMode)
            .WithErrorMessage(ValidationError.Invalid);
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
            Title = new string('a', GameFieldLimits.TitleMaxLength + 1)
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
            SystemName = new string('a', GameFieldLimits.SystemMaxLength + 1)
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
            NarrativeSetting = new string('a', GameFieldLimits.SettingMaxLength + 1)
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
            Info = new string('a', GameFieldLimits.InfoMinLength - 1)
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

    /// <summary>
    /// Whatever creation accepts, an edit of the same game accepts too.
    /// </summary>
    /// <remarks>
    /// Both validators read <see cref="GameFieldLimits" />, or a created game could not be saved again.
    /// </remarks>
    [Fact]
    public async Task AcceptEveryLengthGameCreationAccepts()
    {
        var input = new UpdateGame
        {
            GameId = Guid.NewGuid(),
            Title = new string('a', GameFieldLimits.TitleMaxLength),
            SystemName = new string('a', GameFieldLimits.SystemMaxLength),
            NarrativeSetting = new string('a', GameFieldLimits.SettingMaxLength)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
