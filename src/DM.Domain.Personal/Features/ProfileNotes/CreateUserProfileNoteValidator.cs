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

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(5000).WithMessage(ValidationError.Long);
    }
}
