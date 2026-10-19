using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

internal class UpdateTagGroupValidator : AbstractValidator<UpdateTagGroup>
{
    public UpdateTagGroupValidator()
    {
        RuleFor(g => g.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        Include(new CreateTagGroupValidator());
    }
}
