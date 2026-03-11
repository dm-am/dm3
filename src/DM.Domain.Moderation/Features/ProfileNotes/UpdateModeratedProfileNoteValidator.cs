using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Validator for moderator note update
/// </summary>
internal class UpdateModeratedProfileNoteValidator : AbstractValidator<UpdateModeratedProfileNote>
{
    public UpdateModeratedProfileNoteValidator()
    {
        RuleFor(n => n.NoteId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(n => n.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(5000).WithMessage(ValidationError.Long);
    }
}
