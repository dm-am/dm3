using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Validator for game creation DTO model
/// </summary>
internal class CreateGameValidator : AbstractValidator<CreateGame>
{
    /// <inheritdoc />
    public CreateGameValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(g => g.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long);

        RuleFor(g => g.SystemName)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(50).WithMessage(ValidationError.Long);

        RuleFor(g => g.NarrativeSetting)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(50).WithMessage(ValidationError.Long);

        RuleFor(g => g.Info)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MinimumLength(200).WithMessage(ValidationError.Short);

        When(g => !string.IsNullOrEmpty(g.AssistantUsername), () =>
            RuleFor(g => g.AssistantUsername)
                .MustAsync(userLookupService.UserExists).WithMessage(ValidationError.Invalid));
    }
}