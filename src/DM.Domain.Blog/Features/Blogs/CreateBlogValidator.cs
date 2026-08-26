using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
internal class CreateBlogValidator : AbstractValidator<CreateBlog>
{
    /// <inheritdoc />
    public CreateBlogValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BlogFieldLimits.BlogTitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(x => x.DraftVisibility)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // The blog description renders on the Comment surface, which does not
        // declare [private]: the tag is not markup there and hides nothing.
        RuleFor(x => x.Description)
            .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
            .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Description));
    }
}
