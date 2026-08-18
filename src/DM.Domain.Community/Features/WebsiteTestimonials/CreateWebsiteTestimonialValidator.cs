using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <inheritdoc />
internal class CreateWebsiteTestimonialValidator : AbstractValidator<CreateWebsiteTestimonial>
{
    /// <inheritdoc />
    public CreateWebsiteTestimonialValidator()
    {
        // The author is named by the moderator, so an empty name is a malformed
        // request rather than "sign it with whoever is logged in".
        RuleFor(t => t.AuthorUsername)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(10000).WithMessage(ValidationError.Long);
    }
}
