using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <inheritdoc />
internal class UpdateWebsiteTestimonialValidator : AbstractValidator<UpdateWebsiteTestimonial>
{
    /// <inheritdoc />
    public UpdateWebsiteTestimonialValidator()
    {
        RuleFor(t => t.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(10000).WithMessage(ValidationError.Long);
    }
}
