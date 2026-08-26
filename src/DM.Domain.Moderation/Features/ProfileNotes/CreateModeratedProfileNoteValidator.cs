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
            // 4000 = ModeratedProfileNote.Text column width. Longer input is a
            // Postgres 22001, which surfaces as a 500.
            .MaximumLength(ProfileNoteFieldLimits.ContentMaxLength).WithMessage(ValidationError.Long);
    }
}
