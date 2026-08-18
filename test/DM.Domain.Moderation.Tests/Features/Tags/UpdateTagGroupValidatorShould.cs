using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tags;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tags;

public class UpdateTagGroupValidatorShould : UnitTestBase
{
    private readonly UpdateTagGroupValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = "Genres",
            Description = "Game genres",
            SortOrder = 1
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenIdIsEmpty()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.Empty,
            Title = "Genres"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Id)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
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
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = "Genres",
            SortOrder = -1
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.SortOrder)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// Clearing the number is how a group stops limiting anything, so an absent
    /// value has to survive the update path as well as the creation one.
    /// </summary>
    [Fact]
    public void PassWhenMaxTagsPerGameIsAbsent()
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = "Genres",
            MaxTagsPerGame = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(g => g.MaxTagsPerGame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FailWhenMaxTagsPerGameIsBelowOne(int limit)
    {
        var input = new UpdateTagGroup
        {
            Id = Guid.NewGuid(),
            Title = "Genres",
            MaxTagsPerGame = limit
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(g => g.MaxTagsPerGame)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
