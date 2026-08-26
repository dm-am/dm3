using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Blog.Features.Publications;

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
                .MaximumLength(PublicationFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);
        });

        When(x => x.Content != null, () =>
        {
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage(ValidationError.Empty);

            // The Comment surface does not declare [private], and an edit is
            // the other way the tag gets into a stored publication.
            RuleFor(x => x.Content)
                .Must(content => !PrivateBlockMarkup.ContainsPrivateMarkup(content))
                .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Content));
        });

        When(x => x.Preview != null, () =>
        {
            RuleFor(x => x.Preview)
                .MaximumLength(PublicationFieldLimits.PreviewMaxLength).WithMessage(ValidationError.Long);
        });
    }
}
