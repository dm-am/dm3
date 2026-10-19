using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
internal class UpdateBlogValidator : AbstractValidator<UpdateBlog>
{
    public UpdateBlogValidator()
    {
        RuleFor(x => x.BlogId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(x => x.Title != null, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(BlogFieldLimits.BlogTitleMaxLength).WithMessage(ValidationError.Long);
        });

        // An integer outside the enum is neither Private nor Public and would read as the more open one.
        RuleFor(x => x.DraftVisibility)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // The Comment surface does not declare [private], and an edit is the
        // other way the tag gets into a stored blog description.
        When(x => x.Description != null, () =>
            RuleFor(x => x.Description)
                .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
                .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Description)));
    }
}
