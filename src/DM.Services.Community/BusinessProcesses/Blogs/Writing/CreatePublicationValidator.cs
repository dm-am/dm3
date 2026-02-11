using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <inheritdoc />
internal class CreatePublicationValidator : AbstractValidator<CreatePublication>
{
    /// <inheritdoc />
    public CreatePublicationValidator()
    {
        RuleFor(x => x.BlogId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(300).WithMessage(ValidationError.Long);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(x => x.Preview)
            .MaximumLength(500).WithMessage(ValidationError.Long);
    }
}
