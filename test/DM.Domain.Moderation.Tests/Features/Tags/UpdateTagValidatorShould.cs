using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tags;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tags;

public class UpdateTagValidatorShould : UnitTestBase
{
    private readonly UpdateTagValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = "Fantasy",
            Description = "Fantasy games",
            SortOrder = 1
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenIdIsEmpty()
    {
        var input = new UpdateTag
        {
            Id = Guid.Empty,
            GroupId = Guid.NewGuid(),
            Title = "Fantasy"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.Id)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenGroupIdIsEmpty()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.Empty,
            Title = "Fantasy"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.GroupId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = "Fantasy",
            Description = new string('a', 501)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.Description)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenSortOrderIsNegative()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = "Fantasy",
            SortOrder = -1
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.SortOrder)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// The description is the hint of a tag, where a hidden block is erased for
    /// every reader: refused, with the place and the way to show it as an
    /// example, as on every surface that has none.
    /// </summary>
    [Fact]
    public void FailWhenDescriptionHidesText()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = "ERP",
            Description = "Эротика [private]секрет[/private]"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(t => t.Description)
            .WithErrorMessage("Скрытый текст работает только в игровом тексте поста: [private], символ 9. " +
                              "Чтобы показать разметку примером, поставьте ее между [noparse] и [/noparse].");
    }

    [Fact]
    public void PassForADescriptionWithAHintAndAnExample()
    {
        var input = new UpdateTag
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Title = "ERP",
            Description = "[tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip] " +
                          "[noparse][private]пример[/private][/noparse]"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
