using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Content;
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
    /// The two validators used to carry two sets of numbers: creation took the
    /// shared <see cref="GameFieldLimits" /> (200 / 100 / 100), while the edit
    /// had 100 / 50 / 50 written out by hand. So a game created with a
    /// 150-character title could not be saved again after any edit - refused on
    /// the length of a field the server itself had just accepted, and the author
    /// had no way to learn which field, because the domain answers in codes.
    ///
    /// The direction is not reversible: narrowing creation to the smaller
    /// numbers would make already-created games unsavable, which is the same
    /// defect pointed the other way.
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
