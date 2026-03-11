using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Validator for moderator note creation
/// </summary>
internal class CreateModeratedProfileNoteValidator : AbstractValidator<CreateModeratedProfileNote>
{
    public CreateModeratedProfileNoteValidator()
    {
        RuleFor(n => n.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(n => n.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(5000).WithMessage(ValidationError.Long);
    }
}
