using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
internal class UpdateRubricValidator : AbstractValidator<UpdateRubric>
{
    /// <inheritdoc />
    public UpdateRubricValidator()
    {
        RuleFor(x => x.RubricId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(x => x.Title)
            .MaximumLength(BlogFieldLimits.RubricTitleMaxLength).WithMessage(ValidationError.Long)
            .When(x => x.Title is not null);
    }
}
