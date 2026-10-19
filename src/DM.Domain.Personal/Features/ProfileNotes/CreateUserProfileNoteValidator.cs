using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <inheritdoc />
internal class CreateUserProfileNoteValidator : AbstractValidator<CreateUserProfileNote>
{
    public CreateUserProfileNoteValidator()
    {
        RuleFor(x => x.SubjectUsername)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // No NotEmpty: an empty text deletes the note (IUserProfileNoteService.UpsertNote).
        RuleFor(x => x.Text)
            .MaximumLength(2000).WithMessage(ValidationError.Long);
    }
}
