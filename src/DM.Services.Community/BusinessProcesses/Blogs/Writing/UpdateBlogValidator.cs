using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <inheritdoc />
internal class UpdateBlogValidator : AbstractValidator<UpdateBlog>
{
    /// <inheritdoc />
    public UpdateBlogValidator()
    {
        RuleFor(x => x.BlogId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(x => x.Title != null, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(200).WithMessage(ValidationError.Long);
        });
    }
}
