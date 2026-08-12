using System;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Publications;

public class UpdatePublicationValidatorShould : UnitTestBase
{
    private readonly UpdatePublicationValidator validator;

    public UpdatePublicationValidatorShould()
    {
        validator = new UpdatePublicationValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Title = "Updated Publication",
            Content = "Updated content",
            Preview = "Updated preview",
            IsPublished = true,
            CommentsEnabled = false
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenPublicationIdIsEmpty()
    {
        var input = new UpdatePublication { PublicationId = Guid.Empty };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.PublicationId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmptyString()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Title = ""
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Title = new string('a', 301)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenContentIsEmptyString()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Content = ""
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenPreviewExceedsMaxLength()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Preview = new string('a', 501)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Preview)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenPreviewIsAtMaxLength()
    {
        var input = new UpdatePublication
        {
            PublicationId = Guid.NewGuid(),
            Preview = new string('a', 500)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
