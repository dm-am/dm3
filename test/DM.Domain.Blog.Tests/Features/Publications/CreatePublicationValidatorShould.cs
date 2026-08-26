using System;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Publications;

public class CreatePublicationValidatorShould : UnitTestBase
{
    private readonly CreatePublicationValidator validator;

    public CreatePublicationValidatorShould()
    {
        validator = new CreatePublicationValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreatePublication
        {
            BlogId = Guid.NewGuid(),
            Title = "My Publication",
            Content = "Publication content",
            Preview = "Short preview",
            PublishImmediately = true,
            CommentsEnabled = true
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
            Title = new string('a', 301),
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
            Preview = new string('a', 501)
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
            Preview = new string('a', 500)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Hiding markup on a surface that does not declare it.
    /// </summary>
    /// <remarks>
    /// The tag is not markup here, so it hides nothing: the line the author
    /// wrote for one reader is published with the tag still around it. Refused
    /// at the save path rather than erased, because a draft is not an attack
    /// and a paragraph that vanished without a word is looked for instead of
    /// rewritten.
    /// </remarks>
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
