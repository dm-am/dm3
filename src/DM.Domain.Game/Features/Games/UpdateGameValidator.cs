using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class UpdateGameValidator : AbstractValidator<UpdateGame>
{
    /// <inheritdoc />
    public UpdateGameValidator(
        IUserLookupService userLookupService)
    {
        When(g => g.Title != default, () =>
            RuleFor(g => g.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(100).WithMessage(ValidationError.Long));

        When(g => g.SystemName != default, () =>
            RuleFor(g => g.SystemName)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(50).WithMessage(ValidationError.Long));

        When(g => g.NarrativeSetting != default, () =>
            RuleFor(g => g.NarrativeSetting)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(50).WithMessage(ValidationError.Long));

        When(g => g.Info != default, () =>
            RuleFor(g => g.Info)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MinimumLength(200).WithMessage(ValidationError.Short));

        When(g => g.AssistantUsername != default, () =>
            RuleFor(g => g.AssistantUsername)
                .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
