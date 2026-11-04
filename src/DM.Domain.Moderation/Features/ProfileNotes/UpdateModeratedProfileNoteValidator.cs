using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.ProfileNotes;

internal class UpdateModeratedProfileNoteValidator : AbstractValidator<UpdateModeratedProfileNote>
{
    public UpdateModeratedProfileNoteValidator()
    {
        RuleFor(n => n.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(n => n.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(ProfileNoteFieldLimits.ContentMaxLength).WithMessage(ValidationError.Long);
    }
}
