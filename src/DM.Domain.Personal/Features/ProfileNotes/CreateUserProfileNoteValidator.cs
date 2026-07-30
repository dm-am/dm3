using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <inheritdoc />
internal class CreateUserProfileNoteValidator : AbstractValidator<CreateUserProfileNote>
{
    /// <inheritdoc />
    public CreateUserProfileNoteValidator()
    {
        RuleFor(x => x.SubjectUsername)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // Deliberately no NotEmpty: an empty text is how the caller deletes the
        // note (see IUserProfileNoteService.UpsertNote), and that is covered by a
        // test. 2000 = UserProfileNote.Text column width.
        RuleFor(x => x.Text)
            .MaximumLength(2000).WithMessage(ValidationError.Long);
    }
}
