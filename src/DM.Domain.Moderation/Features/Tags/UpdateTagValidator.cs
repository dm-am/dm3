using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

internal class UpdateTagValidator : AbstractValidator<UpdateTag>
{
    public UpdateTagValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        Include(new CreateTagValidator());
    }
}
