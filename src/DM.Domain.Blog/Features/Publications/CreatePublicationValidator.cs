using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Blog.Features.Publications;

/// <inheritdoc />
internal class CreatePublicationValidator : AbstractValidator<CreatePublication>
{
    public CreatePublicationValidator()
    {
        RuleFor(x => x.BlogId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(PublicationFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(x => x.Content)
            .Must(content => !PrivateBlockMarkup.ContainsPrivateMarkup(content))
            .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Content));

        RuleFor(x => x.Preview)
            .MaximumLength(PublicationFieldLimits.PreviewMaxLength).WithMessage(ValidationError.Long);
    }
}
