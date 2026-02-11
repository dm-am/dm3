using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <inheritdoc />
internal class UpdatePublicationValidator : AbstractValidator<UpdatePublication>
{
    /// <inheritdoc />
    public UpdatePublicationValidator()
    {
        RuleFor(x => x.PublicationId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(x => x.Title != null, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(300).WithMessage(ValidationError.Long);
        });

        When(x => x.Content != null, () =>
        {
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage(ValidationError.Empty);
        });

        When(x => x.Preview != null, () =>
        {
            RuleFor(x => x.Preview)
                .MaximumLength(500).WithMessage(ValidationError.Long);
        });
    }
}
