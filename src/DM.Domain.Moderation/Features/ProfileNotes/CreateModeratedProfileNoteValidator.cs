using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.ProfileNotes;

internal class CreateModeratedProfileNoteValidator : AbstractValidator<CreateModeratedProfileNote>
{
    public CreateModeratedProfileNoteValidator()
    {
        RuleFor(n => n.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(n => n.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(ProfileNoteFieldLimits.ContentMaxLength).WithMessage(ValidationError.Long);
    }
}
