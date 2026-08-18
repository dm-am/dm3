using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tags;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tags;

public class CreateTagGroupValidatorShould : UnitTestBase
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
        var input = new CreateTagGroup { Title = new string('a', 101) };

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
            Description = new string('a', 501)
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

    /// <summary>
    /// A group with no number set limits nothing, and that is what an empty
    /// field means. Zero would mean a group no game can take a tag from.
    /// </summary>
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
