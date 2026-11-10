using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tags;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tags;

public class CreateTagGroupValidatorShould
{
    private readonly CreateTagGroupValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateTagGroup
        {
            Title = "Genres",
            Description = "Game genres",
            SortOrder = 1
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateTagGroup { Title = "" };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateTagGroup { Title = new string('a', TagFieldLimits.TitleMaxLength + 1) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var input = new CreateTagGroup
        {
            Title = "Genres",
            Description = new string('a', TagFieldLimits.DescriptionMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Description)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenSortOrderIsNegative()
    {
        var input = new CreateTagGroup
        {
            Title = "Genres",
            SortOrder = -1
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.SortOrder)
            .WithErrorMessage(ValidationError.Invalid);
    }

    // Zero would mean a group no game can take a tag from.
    [Fact]
    public void PassWhenMaxTagsPerGameIsAbsent()
    {
        var input = new CreateTagGroup { Title = "Genres", MaxTagsPerGame = null };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(g => g.MaxTagsPerGame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FailWhenMaxTagsPerGameIsBelowOne(int limit)
    {
        var input = new CreateTagGroup { Title = "Genres", MaxTagsPerGame = limit };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.MaxTagsPerGame)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
