using System;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Publications;

public class CreatePublicationValidatorShould
{
    private readonly CreatePublicationValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "My Publication",
            Content = "Publication content",
            Preview = "Short preview"
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenBlogIdIsEmpty()
    {
        var input = new CreatePublication { BlogId = Guid.Empty };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BlogId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "",
            Content = "Content"
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = new string('a', PublicationFieldLimits.TitleMaxLength + 1),
            Content = "Content"
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenContentIsEmpty()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "Title",
            Content = ""
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenPreviewExceedsMaxLength()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "Title",
            Content = "Content",
            Preview = new string('a', PublicationFieldLimits.PreviewMaxLength + 1)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Preview)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenPreviewIsAtMaxLength()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "Title",
            Content = "Content",
            Preview = new string('a', PublicationFieldLimits.PreviewMaxLength)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // Refused rather than erased: a paragraph that vanished without a word is looked for, not
    // rewritten.
    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "Title",
            Content = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }
}
